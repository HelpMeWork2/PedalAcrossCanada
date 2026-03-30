using PedalAcrossCanada.Server.Domain.Entities;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Tests.Helpers;

/// <summary>
/// Convenience factory for building domain entities with sensible defaults for tests.
/// </summary>
public static class TestDataBuilder
{
    public static Event CreateActiveEvent(
        Guid? id = null,
        bool stravaEnabled = true,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var now = DateTime.UtcNow;
        return new Event
        {
            Id = id ?? Guid.NewGuid(),
            Name = "Test Cycling Event",
            Description = "A test event",
            StartDate = startDate ?? now.AddDays(-30),
            EndDate = endDate ?? now.AddDays(30),
            RouteDistanceKm = 3757m,
            Status = EventStatus.Active,
            StravaEnabled = stravaEnabled,
            MaxSingleRideKm = 300m,
            LeaderboardPublic = true,
            ShowTeamAverage = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public static Event CreateDraftEvent(Guid? id = null)
    {
        var evt = CreateActiveEvent(id);
        evt.Status = EventStatus.Draft;
        return evt;
    }

    public static Participant CreateParticipant(
        Guid eventId,
        Guid? id = null,
        string? userId = null)
    {
        var now = DateTime.UtcNow;
        return new Participant
        {
            Id = id ?? Guid.NewGuid(),
            EventId = eventId,
            UserId = userId ?? Guid.NewGuid().ToString(),
            FirstName = "Test",
            LastName = "Rider",
            WorkEmail = $"test-{Guid.NewGuid():N}@example.com",
            DisplayName = "Test Rider",
            Status = ParticipantStatus.Active,
            JoinedAt = now,
            LeaderboardOptIn = true,
            StravaConsentGiven = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public static ExternalConnection CreateStravaConnection(
        Guid participantId,
        string encryptedTokenData,
        ConnectionStatus status = ConnectionStatus.Connected)
    {
        return new ExternalConnection
        {
            Id = Guid.NewGuid(),
            ParticipantId = participantId,
            Provider = "Strava",
            ExternalAthleteId = "12345",
            EncryptedTokenData = encryptedTokenData,
            ConnectionStatus = status,
            ConnectedAt = DateTime.UtcNow
        };
    }

    public static Activity CreateStravaActivity(
        Guid participantId,
        Guid eventId,
        string externalActivityId,
        decimal distanceKm = 25.5m)
    {
        return new Activity
        {
            Id = Guid.NewGuid(),
            ParticipantId = participantId,
            EventId = eventId,
            ActivityDate = DateTime.UtcNow.AddDays(-1),
            DistanceKm = distanceKm,
            RideType = RideType.Other,
            Source = ActivitySource.Strava,
            Status = ActivityStatus.Approved,
            CountsTowardTotal = true,
            ExternalActivityId = externalActivityId,
            ExternalTitle = "Morning Ride",
            ImportedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
