using System.Text.Json;
using Microsoft.Extensions.Options;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Server.Application.Services;
using PedalAcrossCanada.Server.Configuration;
using PedalAcrossCanada.Server.Tests.Fakes;
using PedalAcrossCanada.Server.Tests.Helpers;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Tests.Strava;

public sealed class StravaTokenServiceTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory;
    private readonly FakeAuditService _auditService;
    private readonly FakeTokenEncryptionService _encryptionService;
    private readonly IOptions<StravaSettings> _settings;

    public StravaTokenServiceTests()
    {
        _dbFactory = new TestDbContextFactory();
        _auditService = new FakeAuditService();
        _encryptionService = new FakeTokenEncryptionService();
        _settings = Options.Create(new StravaSettings
        {
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            RedirectUri = "https://localhost/strava/callback"
        });
    }

    public void Dispose() => _dbFactory.Dispose();

    private StravaTokenService CreateService() =>
        new(_dbFactory.CreateContext(), _encryptionService, _auditService, _settings);

    #region BuildAuthorizationUrl

    [Fact]
    public void BuildAuthorizationUrl_ReturnsCorrectFormat()
    {
        var sut = CreateService();
        var participantId = Guid.NewGuid();

        var url = sut.BuildAuthorizationUrl(participantId);

        Assert.Contains("https://www.strava.com/oauth/authorize", url);
        Assert.Contains("client_id=test-client-id", url);
        Assert.Contains($"state={participantId}", url);
        Assert.Contains("response_type=code", url);
        Assert.Contains("scope=read,activity:read", url);
    }

    [Fact]
    public void BuildAuthorizationUrl_EncodesRedirectUri()
    {
        var sut = CreateService();

        var url = sut.BuildAuthorizationUrl(Guid.NewGuid());

        Assert.Contains("redirect_uri=https%3A%2F%2Flocalhost%2Fstrava%2Fcallback", url);
    }

    #endregion

    #region DisconnectAsync

    [Fact]
    public async Task DisconnectAsync_SetsStatusToDisconnected()
    {
        // Arrange
        var evt = TestDataBuilder.CreateActiveEvent();
        var participant = TestDataBuilder.CreateParticipant(evt.Id);
        var tokenData = new StravaTokenData
        {
            AccessToken = "access",
            RefreshToken = "refresh",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
            AthleteId = "99"
        };
        var encrypted = _encryptionService.Encrypt(JsonSerializer.Serialize(tokenData));
        var connection = TestDataBuilder.CreateStravaConnection(participant.Id, encrypted);

        await using (var arrangeCtx = _dbFactory.CreateContext())
        {
            arrangeCtx.Events.Add(evt);
            arrangeCtx.Participants.Add(participant);
            arrangeCtx.ExternalConnections.Add(connection);
            await arrangeCtx.SaveChangesAsync();
        }

        // Act
        var sut = CreateService();
        await sut.DisconnectAsync(participant.Id, "test-user");

        // Assert
        await using var assertCtx = _dbFactory.CreateContext();
        var updated = await assertCtx.ExternalConnections.FindAsync(connection.Id);
        Assert.NotNull(updated);
        Assert.Equal(ConnectionStatus.Disconnected, updated.ConnectionStatus);
        Assert.NotNull(updated.DisconnectedAt);
        Assert.Equal(string.Empty, updated.EncryptedTokenData);
    }

    [Fact]
    public async Task DisconnectAsync_CreatesAuditEntry()
    {
        // Arrange
        var evt = TestDataBuilder.CreateActiveEvent();
        var participant = TestDataBuilder.CreateParticipant(evt.Id);
        var encrypted = _encryptionService.Encrypt(JsonSerializer.Serialize(new StravaTokenData
        {
            AccessToken = "a", RefreshToken = "r", ExpiresAt = 0, AthleteId = "1"
        }));
        var connection = TestDataBuilder.CreateStravaConnection(participant.Id, encrypted);

        await using (var ctx = _dbFactory.CreateContext())
        {
            ctx.Events.Add(evt);
            ctx.Participants.Add(participant);
            ctx.ExternalConnections.Add(connection);
            await ctx.SaveChangesAsync();
        }

        // Act
        var sut = CreateService();
        await sut.DisconnectAsync(participant.Id, "actor-123");

        // Assert
        Assert.Single(_auditService.Entries);
        Assert.Equal("StravaDisconnected", _auditService.Entries[0].Action);
        Assert.Equal("actor-123", _auditService.Entries[0].Actor);
    }

    [Fact]
    public async Task DisconnectAsync_WhenNoConnection_ThrowsKeyNotFoundException()
    {
        var sut = CreateService();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => sut.DisconnectAsync(Guid.NewGuid(), "user"));
    }

    #endregion

    #region GetStatusAsync

    [Fact]
    public async Task GetStatusAsync_WhenConnected_ReturnsIsConnectedTrue()
    {
        // Arrange
        var evt = TestDataBuilder.CreateActiveEvent();
        var participant = TestDataBuilder.CreateParticipant(evt.Id);
        var encrypted = _encryptionService.Encrypt("{\"AccessToken\":\"a\"}");
        var connection = TestDataBuilder.CreateStravaConnection(participant.Id, encrypted);

        await using (var ctx = _dbFactory.CreateContext())
        {
            ctx.Events.Add(evt);
            ctx.Participants.Add(participant);
            ctx.ExternalConnections.Add(connection);
            await ctx.SaveChangesAsync();
        }

        // Act
        var sut = CreateService();
        var status = await sut.GetStatusAsync(participant.Id);

        // Assert
        Assert.True(status.IsConnected);
        Assert.Equal(ConnectionStatus.Connected, status.ConnectionStatus);
        Assert.Equal("12345", status.ExternalAthleteId);
    }

    [Fact]
    public async Task GetStatusAsync_WhenNoConnection_ReturnsIsConnectedFalse()
    {
        var sut = CreateService();

        var status = await sut.GetStatusAsync(Guid.NewGuid());

        Assert.False(status.IsConnected);
    }

    [Fact]
    public async Task GetStatusAsync_WhenDisconnected_ReturnsIsConnectedFalse()
    {
        // Arrange
        var evt = TestDataBuilder.CreateActiveEvent();
        var participant = TestDataBuilder.CreateParticipant(evt.Id);
        var connection = TestDataBuilder.CreateStravaConnection(
            participant.Id, "data", ConnectionStatus.Disconnected);

        await using (var ctx = _dbFactory.CreateContext())
        {
            ctx.Events.Add(evt);
            ctx.Participants.Add(participant);
            ctx.ExternalConnections.Add(connection);
            await ctx.SaveChangesAsync();
        }

        // Act
        var sut = CreateService();
        var status = await sut.GetStatusAsync(participant.Id);

        // Assert
        Assert.False(status.IsConnected);
        Assert.Equal(ConnectionStatus.Disconnected, status.ConnectionStatus);
    }

    #endregion

    #region GetTokenDataAsync

    [Fact]
    public async Task GetTokenDataAsync_WhenConnected_ReturnsDecryptedTokenData()
    {
        // Arrange
        var evt = TestDataBuilder.CreateActiveEvent();
        var participant = TestDataBuilder.CreateParticipant(evt.Id);
        var tokenData = new StravaTokenData
        {
            AccessToken = "access-token-123",
            RefreshToken = "refresh-token-456",
            ExpiresAt = 1700000000,
            AthleteId = "athlete-789"
        };
        var encrypted = _encryptionService.Encrypt(JsonSerializer.Serialize(tokenData));
        var connection = TestDataBuilder.CreateStravaConnection(participant.Id, encrypted);

        await using (var ctx = _dbFactory.CreateContext())
        {
            ctx.Events.Add(evt);
            ctx.Participants.Add(participant);
            ctx.ExternalConnections.Add(connection);
            await ctx.SaveChangesAsync();
        }

        // Act
        var sut = CreateService();
        var result = await sut.GetTokenDataAsync(participant.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("access-token-123", result.AccessToken);
        Assert.Equal("refresh-token-456", result.RefreshToken);
        Assert.Equal(1700000000, result.ExpiresAt);
        Assert.Equal("athlete-789", result.AthleteId);
    }

    [Fact]
    public async Task GetTokenDataAsync_WhenDisconnected_ReturnsNull()
    {
        // Arrange
        var evt = TestDataBuilder.CreateActiveEvent();
        var participant = TestDataBuilder.CreateParticipant(evt.Id);
        var connection = TestDataBuilder.CreateStravaConnection(
            participant.Id, "data", ConnectionStatus.Disconnected);

        await using (var ctx = _dbFactory.CreateContext())
        {
            ctx.Events.Add(evt);
            ctx.Participants.Add(participant);
            ctx.ExternalConnections.Add(connection);
            await ctx.SaveChangesAsync();
        }

        // Act
        var sut = CreateService();
        var result = await sut.GetTokenDataAsync(participant.Id);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetTokenDataAsync_WhenNoConnection_ReturnsNull()
    {
        var sut = CreateService();

        var result = await sut.GetTokenDataAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    #endregion

    #region UpdateTokenDataAsync

    [Fact]
    public async Task UpdateTokenDataAsync_UpdatesEncryptedData()
    {
        // Arrange
        var evt = TestDataBuilder.CreateActiveEvent();
        var participant = TestDataBuilder.CreateParticipant(evt.Id);
        var original = _encryptionService.Encrypt("{\"AccessToken\":\"old\"}");
        var connection = TestDataBuilder.CreateStravaConnection(participant.Id, original);

        await using (var ctx = _dbFactory.CreateContext())
        {
            ctx.Events.Add(evt);
            ctx.Participants.Add(participant);
            ctx.ExternalConnections.Add(connection);
            await ctx.SaveChangesAsync();
        }

        var newTokenData = new StravaTokenData
        {
            AccessToken = "new-access",
            RefreshToken = "new-refresh",
            ExpiresAt = 9999999999,
            AthleteId = "athlete-1"
        };

        // Act
        var sut = CreateService();
        await sut.UpdateTokenDataAsync(participant.Id, newTokenData);

        // Assert
        await using var assertCtx = _dbFactory.CreateContext();
        var updated = await assertCtx.ExternalConnections.FindAsync(connection.Id);
        Assert.NotNull(updated);
        var decrypted = JsonSerializer.Deserialize<StravaTokenData>(
            _encryptionService.Decrypt(updated.EncryptedTokenData));
        Assert.NotNull(decrypted);
        Assert.Equal("new-access", decrypted.AccessToken);
        Assert.Equal("new-refresh", decrypted.RefreshToken);
    }

    [Fact]
    public async Task UpdateTokenDataAsync_WhenNoConnection_ThrowsKeyNotFoundException()
    {
        var sut = CreateService();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => sut.UpdateTokenDataAsync(Guid.NewGuid(), new StravaTokenData()));
    }

    #endregion

    #region Token Encryption Round-Trip

    [Fact]
    public void TokenEncryption_RoundTrip_PreservesTokenData()
    {
        var tokenData = new StravaTokenData
        {
            AccessToken = "abc123",
            RefreshToken = "def456",
            ExpiresAt = 1700000000,
            AthleteId = "42"
        };

        var json = JsonSerializer.Serialize(tokenData);
        var encrypted = _encryptionService.Encrypt(json);
        var decrypted = _encryptionService.Decrypt(encrypted);
        var result = JsonSerializer.Deserialize<StravaTokenData>(decrypted);

        Assert.NotNull(result);
        Assert.Equal(tokenData.AccessToken, result.AccessToken);
        Assert.Equal(tokenData.RefreshToken, result.RefreshToken);
        Assert.Equal(tokenData.ExpiresAt, result.ExpiresAt);
        Assert.Equal(tokenData.AthleteId, result.AthleteId);
    }

    [Fact]
    public void TokenEncryption_EncryptedValueDiffersFromPlaintext()
    {
        var plain = "sensitive-token-data";

        var encrypted = _encryptionService.Encrypt(plain);

        Assert.NotEqual(plain, encrypted);
    }

    #endregion
}
