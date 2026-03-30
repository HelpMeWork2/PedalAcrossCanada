using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Server.Application.Services;
using PedalAcrossCanada.Server.Tests.Fakes;
using PedalAcrossCanada.Server.Tests.Helpers;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Tests.Strava;

public sealed class StravaSyncServiceTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory;
    private readonly FakeAuditService _auditService;
    private readonly FakeTokenEncryptionService _encryptionService;
    private readonly FakeStravaApiClient _stravaApiClient;
    private readonly NullLogger<StravaSyncService> _logger = new();

    public StravaSyncServiceTests()
    {
        _dbFactory = new TestDbContextFactory();
        _auditService = new FakeAuditService();
        _encryptionService = new FakeTokenEncryptionService();
        _stravaApiClient = new FakeStravaApiClient();
    }

    public void Dispose() => _dbFactory.Dispose();

    private StravaSyncService CreateService()
    {
        var ctx = _dbFactory.CreateContext();
        var tokenService = new StravaTokenService(
            _dbFactory.CreateContext(),
            _encryptionService,
            _auditService,
            Microsoft.Extensions.Options.Options.Create(new Server.Configuration.StravaSettings
            {
                ClientId = "test",
                ClientSecret = "secret",
                RedirectUri = "https://localhost/callback"
            }));

        return new StravaSyncService(ctx, tokenService, _stravaApiClient, _auditService, _logger);
    }

    private StravaTokenData CreateValidTokenData(long? expiresAt = null)
    {
        return new StravaTokenData
        {
            AccessToken = "valid-access-token",
            RefreshToken = "valid-refresh-token",
            ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
            AthleteId = "12345"
        };
    }

    private async Task<(Guid EventId, Guid ParticipantId)> SeedParticipantWithConnectionAsync(
        StravaTokenData? tokenData = null,
        bool stravaEnabled = true,
        EventStatus eventStatus = EventStatus.Active,
        DateTime? eventStart = null,
        DateTime? eventEnd = null)
    {
        var token = tokenData ?? CreateValidTokenData();
        var encrypted = _encryptionService.Encrypt(JsonSerializer.Serialize(token));

        var evt = TestDataBuilder.CreateActiveEvent(stravaEnabled: stravaEnabled, startDate: eventStart, endDate: eventEnd);
        evt.Status = eventStatus;
        var participant = TestDataBuilder.CreateParticipant(evt.Id);
        var connection = TestDataBuilder.CreateStravaConnection(participant.Id, encrypted);

        await using var ctx = _dbFactory.CreateContext();
        ctx.Events.Add(evt);
        ctx.Participants.Add(participant);
        ctx.ExternalConnections.Add(connection);
        await ctx.SaveChangesAsync();

        return (evt.Id, participant.Id);
    }

    #region Happy Path

    [Fact]
    public async Task SyncParticipantAsync_ImportsNewActivities()
    {
        // Arrange
        var (eventId, participantId) = await SeedParticipantWithConnectionAsync();

        _stravaApiClient.ActivitiesByPage[1] =
        [
            new StravaActivityData
            {
                Id = 1001,
                Name = "Morning Ride",
                Type = "Ride",
                Distance = 25500f, // 25.5 km
                StartDateLocal = DateTime.UtcNow.AddDays(-5)
            },
            new StravaActivityData
            {
                Id = 1002,
                Name = "Evening Ride",
                Type = "Ride",
                Distance = 15000f, // 15 km
                StartDateLocal = DateTime.UtcNow.AddDays(-3)
            }
        ];

        // Act
        var sut = CreateService();
        var result = await sut.SyncParticipantAsync(participantId, "actor");

        // Assert
        Assert.Equal(2, result.ImportedCount);
        Assert.Equal(0, result.SkippedDuplicateCount);
        Assert.Equal(0, result.SkippedOutOfRangeCount);
        Assert.Null(result.ErrorMessage);

        // Verify activities persisted
        await using var assertCtx = _dbFactory.CreateContext();
        var activities = assertCtx.Activities
            .Where(a => a.ParticipantId == participantId)
            .ToList();
        Assert.Equal(2, activities.Count);
        Assert.All(activities, a =>
        {
            Assert.Equal(ActivitySource.Strava, a.Source);
            Assert.Equal(ActivityStatus.Approved, a.Status);
            Assert.True(a.CountsTowardTotal);
        });
    }

    #endregion

    #region Duplicate Handling

    [Fact]
    public async Task SyncParticipantAsync_SkipsExistingExternalActivityIds()
    {
        // Arrange
        var (eventId, participantId) = await SeedParticipantWithConnectionAsync();

        // Pre-existing activity with external id "1001"
        await using (var ctx = _dbFactory.CreateContext())
        {
            ctx.Activities.Add(TestDataBuilder.CreateStravaActivity(participantId, eventId, "1001"));
            await ctx.SaveChangesAsync();
        }

        _stravaApiClient.ActivitiesByPage[1] =
        [
            new StravaActivityData { Id = 1001, Name = "Duplicate", Type = "Ride", Distance = 25000f, StartDateLocal = DateTime.UtcNow.AddDays(-2) },
            new StravaActivityData { Id = 1002, Name = "New Ride", Type = "Ride", Distance = 10000f, StartDateLocal = DateTime.UtcNow.AddDays(-1) }
        ];

        // Act
        var sut = CreateService();
        var result = await sut.SyncParticipantAsync(participantId, "actor");

        // Assert
        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.SkippedDuplicateCount);
    }

    [Fact]
    public async Task SyncParticipantAsync_SkipsDuplicatesWithinSameBatch()
    {
        // Arrange - Strava returns same activity id twice (shouldn't happen but defensive)
        var (_, participantId) = await SeedParticipantWithConnectionAsync();

        _stravaApiClient.ActivitiesByPage[1] =
        [
            new StravaActivityData { Id = 2001, Name = "Ride A", Type = "Ride", Distance = 10000f, StartDateLocal = DateTime.UtcNow.AddDays(-1) },
            new StravaActivityData { Id = 2001, Name = "Ride A Dup", Type = "Ride", Distance = 10000f, StartDateLocal = DateTime.UtcNow.AddDays(-1) }
        ];

        // Act
        var sut = CreateService();
        var result = await sut.SyncParticipantAsync(participantId, "actor");

        // Assert - first one imported, second skipped as duplicate
        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.SkippedDuplicateCount);
    }

    #endregion

    #region Meter-to-Km Conversion

    [Theory]
    [InlineData(25500f, 25.5)]    // Standard
    [InlineData(1000f, 1.0)]      // Exact 1 km
    [InlineData(1234f, 1.23)]     // Rounds down
    [InlineData(1235f, 1.24)]     // Rounds to nearest (AwayFromZero: 1.235 -> 1.24)
    [InlineData(1245f, 1.25)]     // 1.245 -> 1.25 (AwayFromZero)
    [InlineData(100000f, 100.0)]  // Century ride
    public async Task SyncParticipantAsync_ConvertsMetersToKmCorrectly(float meters, decimal expectedKm)
    {
        // Arrange
        var (_, participantId) = await SeedParticipantWithConnectionAsync();

        _stravaApiClient.ActivitiesByPage[1] =
        [
            new StravaActivityData { Id = 5001, Name = "Test", Type = "Ride", Distance = meters, StartDateLocal = DateTime.UtcNow.AddDays(-1) }
        ];

        // Act
        var sut = CreateService();
        await sut.SyncParticipantAsync(participantId, "actor");

        // Assert
        await using var ctx = _dbFactory.CreateContext();
        var activity = ctx.Activities.FirstOrDefault(a => a.ExternalActivityId == "5001");
        Assert.NotNull(activity);
        Assert.Equal(expectedKm, activity.DistanceKm);
    }

    [Fact]
    public async Task SyncParticipantAsync_SkipsZeroDistanceActivities()
    {
        var (_, participantId) = await SeedParticipantWithConnectionAsync();

        _stravaApiClient.ActivitiesByPage[1] =
        [
            new StravaActivityData { Id = 6001, Name = "Stationary", Type = "Ride", Distance = 0f, StartDateLocal = DateTime.UtcNow.AddDays(-1) }
        ];

        var sut = CreateService();
        var result = await sut.SyncParticipantAsync(participantId, "actor");

        Assert.Equal(0, result.ImportedCount);
        Assert.Equal(1, result.SkippedOutOfRangeCount);
    }

    #endregion

    #region Activity Type Filtering

    [Theory]
    [InlineData("Ride", true)]
    [InlineData("VirtualRide", true)]
    [InlineData("EBikeRide", true)]
    [InlineData("Run", false)]
    [InlineData("Swim", false)]
    [InlineData("Walk", false)]
    [InlineData("Hike", false)]
    public async Task SyncParticipantAsync_FiltersActivityTypes(string activityType, bool shouldImport)
    {
        var (_, participantId) = await SeedParticipantWithConnectionAsync();

        _stravaApiClient.ActivitiesByPage[1] =
        [
            new StravaActivityData { Id = 7001, Name = "Activity", Type = activityType, Distance = 10000f, StartDateLocal = DateTime.UtcNow.AddDays(-1) }
        ];

        var sut = CreateService();
        var result = await sut.SyncParticipantAsync(participantId, "actor");

        Assert.Equal(shouldImport ? 1 : 0, result.ImportedCount);
        if (!shouldImport)
            Assert.True(result.SkippedOutOfRangeCount > 0);
    }

    #endregion

    #region Date Range Filtering

    [Fact]
    public async Task SyncParticipantAsync_SkipsActivitiesOutsideEventDateRange()
    {
        var start = DateTime.UtcNow.AddDays(-10);
        var end = DateTime.UtcNow.AddDays(-1);
        var (_, participantId) = await SeedParticipantWithConnectionAsync(eventStart: start, eventEnd: end);

        _stravaApiClient.ActivitiesByPage[1] =
        [
            new StravaActivityData
            {
                Id = 8001, Name = "Before Event", Type = "Ride", Distance = 10000f,
                StartDateLocal = start.AddDays(-5) // Before event start
            },
            new StravaActivityData
            {
                Id = 8002, Name = "After Event", Type = "Ride", Distance = 10000f,
                StartDateLocal = end.AddDays(5) // After event end
            },
            new StravaActivityData
            {
                Id = 8003, Name = "In Range", Type = "Ride", Distance = 10000f,
                StartDateLocal = start.AddDays(3) // Within event range
            }
        ];

        var sut = CreateService();
        var result = await sut.SyncParticipantAsync(participantId, "actor");

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(2, result.SkippedOutOfRangeCount);
    }

    #endregion

    #region Token Refresh

    [Fact]
    public async Task SyncParticipantAsync_RefreshesExpiredToken()
    {
        // Arrange - token already expired
        var expiredToken = CreateValidTokenData(expiresAt: DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds());
        var (_, participantId) = await SeedParticipantWithConnectionAsync(tokenData: expiredToken);

        _stravaApiClient.RefreshResult = new StravaTokenRefreshResult
        {
            Success = true,
            AccessToken = "new-access-token",
            RefreshToken = "new-refresh-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(6).ToUnixTimeSeconds()
        };

        _stravaApiClient.ActivitiesByPage[1] =
        [
            new StravaActivityData { Id = 9001, Name = "Post-Refresh Ride", Type = "Ride", Distance = 20000f, StartDateLocal = DateTime.UtcNow.AddDays(-2) }
        ];

        // Act
        var sut = CreateService();
        var result = await sut.SyncParticipantAsync(participantId, "actor");

        // Assert
        Assert.Equal(1, result.ImportedCount);
        Assert.Single(_stravaApiClient.RefreshTokenCalls);
        Assert.Equal("valid-refresh-token", _stravaApiClient.RefreshTokenCalls[0]);

        // Verify the API was called with the new access token
        Assert.Single(_stravaApiClient.GetActivitiesCalls);
        Assert.Equal("new-access-token", _stravaApiClient.GetActivitiesCalls[0].AccessToken);
    }

    [Fact]
    public async Task SyncParticipantAsync_WhenRefreshFails_SetsRequiresReauth()
    {
        // Arrange
        var expiredToken = CreateValidTokenData(expiresAt: DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds());
        var (_, participantId) = await SeedParticipantWithConnectionAsync(tokenData: expiredToken);

        _stravaApiClient.RefreshResult = new StravaTokenRefreshResult
        {
            Success = false,
            Error = "invalid_grant"
        };

        // Act
        var sut = CreateService();
        var result = await sut.SyncParticipantAsync(participantId, "actor");

        // Assert
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("reconnect", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Verify connection status changed
        await using var ctx = _dbFactory.CreateContext();
        var connection = ctx.ExternalConnections
            .FirstOrDefault(ec => ec.ParticipantId == participantId);
        Assert.NotNull(connection);
        Assert.Equal(ConnectionStatus.RequiresReauth, connection.ConnectionStatus);
    }

    #endregion

    #region Event Validation

    [Fact]
    public async Task SyncParticipantAsync_WhenEventNotActive_ThrowsInvalidOperation()
    {
        var (_, participantId) = await SeedParticipantWithConnectionAsync(eventStatus: EventStatus.Draft);

        var sut = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.SyncParticipantAsync(participantId, "actor"));
    }

    [Fact]
    public async Task SyncParticipantAsync_WhenEventClosed_ThrowsInvalidOperation()
    {
        var (_, participantId) = await SeedParticipantWithConnectionAsync(eventStatus: EventStatus.Closed);

        var sut = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.SyncParticipantAsync(participantId, "actor"));
    }

    [Fact]
    public async Task SyncParticipantAsync_WhenStravaNotEnabled_ThrowsInvalidOperation()
    {
        var (_, participantId) = await SeedParticipantWithConnectionAsync(stravaEnabled: false);

        var sut = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.SyncParticipantAsync(participantId, "actor"));
    }

    [Fact]
    public async Task SyncParticipantAsync_WhenParticipantNotFound_ThrowsKeyNotFound()
    {
        var sut = CreateService();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => sut.SyncParticipantAsync(Guid.NewGuid(), "actor"));
    }

    #endregion

    #region Not Connected

    [Fact]
    public async Task SyncParticipantAsync_WhenNotConnected_ReturnsError()
    {
        // Arrange - participant exists but no external connection
        var evt = TestDataBuilder.CreateActiveEvent();
        var participant = TestDataBuilder.CreateParticipant(evt.Id);

        await using (var ctx = _dbFactory.CreateContext())
        {
            ctx.Events.Add(evt);
            ctx.Participants.Add(participant);
            await ctx.SaveChangesAsync();
        }

        var sut = CreateService();
        var result = await sut.SyncParticipantAsync(participant.Id, "actor");

        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Not connected", result.ErrorMessage);
    }

    #endregion

    #region API Failure

    [Fact]
    public async Task SyncParticipantAsync_WhenStravaApiThrows_ReturnsErrorResult()
    {
        var (_, participantId) = await SeedParticipantWithConnectionAsync();
        _stravaApiClient.GetActivitiesException = new HttpRequestException("Strava API is down");

        var sut = CreateService();
        var result = await sut.SyncParticipantAsync(participantId, "actor");

        Assert.NotNull(result.ErrorMessage);
        Assert.Equal(1, result.ErrorCount);
    }

    #endregion

    #region Audit Logging

    [Fact]
    public async Task SyncParticipantAsync_CreatesAuditEntry_OnSuccess()
    {
        var (_, participantId) = await SeedParticipantWithConnectionAsync();

        _stravaApiClient.ActivitiesByPage[1] =
        [
            new StravaActivityData { Id = 3001, Name = "Ride", Type = "Ride", Distance = 10000f, StartDateLocal = DateTime.UtcNow.AddDays(-1) }
        ];

        var sut = CreateService();
        await sut.SyncParticipantAsync(participantId, "sync-actor");

        var syncAudit = _auditService.Entries.FirstOrDefault(e => e.Action == "StravaSync");
        Assert.NotNull(syncAudit);
        Assert.Equal("sync-actor", syncAudit.Actor);
        Assert.Contains("Imported: 1", syncAudit.AfterSummary);
    }

    #endregion

    #region Pagination

    [Fact]
    public async Task SyncParticipantAsync_PaginatesThroughStravaApi()
    {
        var (_, participantId) = await SeedParticipantWithConnectionAsync();

        // Page 1: full page of 100
        _stravaApiClient.ActivitiesByPage[1] = Enumerable.Range(1, 100)
            .Select(i => new StravaActivityData
            {
                Id = i,
                Name = $"Ride {i}",
                Type = "Ride",
                Distance = 10000f,
                StartDateLocal = DateTime.UtcNow.AddDays(-1)
            }).ToList();

        // Page 2: partial page (signals end)
        _stravaApiClient.ActivitiesByPage[2] =
        [
            new StravaActivityData { Id = 101, Name = "Last Ride", Type = "Ride", Distance = 5000f, StartDateLocal = DateTime.UtcNow.AddDays(-1) }
        ];

        var sut = CreateService();
        var result = await sut.SyncParticipantAsync(participantId, "actor");

        Assert.Equal(101, result.ImportedCount);
        Assert.Equal(2, _stravaApiClient.GetActivitiesCalls.Count);
        Assert.Equal(1, _stravaApiClient.GetActivitiesCalls[0].Page);
        Assert.Equal(2, _stravaApiClient.GetActivitiesCalls[1].Page);
    }

    [Fact]
    public async Task SyncParticipantAsync_StopsPaginating_WhenEmptyPageReturned()
    {
        var (_, participantId) = await SeedParticipantWithConnectionAsync();

        // Page 1 returns activities, page 2 is empty
        _stravaApiClient.ActivitiesByPage[1] =
        [
            new StravaActivityData { Id = 4001, Name = "Ride", Type = "Ride", Distance = 10000f, StartDateLocal = DateTime.UtcNow.AddDays(-1) }
        ];
        // Page 2 not configured — returns empty by default

        var sut = CreateService();
        var result = await sut.SyncParticipantAsync(participantId, "actor");

        // Should stop after page 1 since it had fewer than 100 results
        Assert.Equal(1, result.ImportedCount);
        Assert.Single(_stravaApiClient.GetActivitiesCalls);
    }

    #endregion

    #region LastSyncAt Update

    [Fact]
    public async Task SyncParticipantAsync_UpdatesLastSyncTimestamp()
    {
        var (_, participantId) = await SeedParticipantWithConnectionAsync();

        _stravaApiClient.ActivitiesByPage[1] =
        [
            new StravaActivityData { Id = 10001, Name = "Ride", Type = "Ride", Distance = 10000f, StartDateLocal = DateTime.UtcNow.AddDays(-1) }
        ];

        var sut = CreateService();
        await sut.SyncParticipantAsync(participantId, "actor");

        await using var ctx = _dbFactory.CreateContext();
        var connection = ctx.ExternalConnections.FirstOrDefault(ec => ec.ParticipantId == participantId);
        Assert.NotNull(connection);
        Assert.NotNull(connection.LastSyncAt);
    }

    #endregion
}
