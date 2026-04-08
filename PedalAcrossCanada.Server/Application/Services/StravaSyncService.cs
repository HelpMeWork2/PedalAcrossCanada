using Microsoft.EntityFrameworkCore;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Server.Domain.Entities;
using PedalAcrossCanada.Server.Infrastructure.Data;
using PedalAcrossCanada.Shared.DTOs.Strava;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Application.Services;

public class StravaSyncService(
    AppDbContext dbContext,
    IStravaTokenService stravaTokenService,
    IStravaApiClient stravaApiClient,
    IAuditService auditService,
    IMilestoneCalculationService milestoneCalculationService,
    IBadgeService badgeService,
    ILogger<StravaSyncService> logger) : IStravaSyncService
{
    private static readonly HashSet<string> SupportedActivityTypes = ["Ride", "VirtualRide", "EBikeRide"];

    public async Task<StravaSyncResultDto> SyncParticipantAsync(Guid participantId, string actor)
    {
        var participant = await dbContext.Participants
            .AsNoTracking()
            .Include(p => p.Event)
            .FirstOrDefaultAsync(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant '{participantId}' not found.");

        if (participant.Event.Status != EventStatus.Active)
        {
            throw new InvalidOperationException("Can only sync activities for an active event.");
        }

        if (!participant.Event.StravaEnabled)
        {
            throw new InvalidOperationException("Strava integration is not enabled for this event.");
        }

        var tokenData = await stravaTokenService.GetTokenDataAsync(participantId);
        if (tokenData is null)
        {
            return new StravaSyncResultDto { ErrorMessage = "Not connected to Strava." };
        }

        // Refresh token if expired
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (tokenData.ExpiresAt <= now)
        {
            var refreshResult = await stravaApiClient.RefreshTokenAsync(tokenData.RefreshToken);
            if (!refreshResult.Success)
            {
                // Mark connection as requiring reauth
                var connection = await dbContext.ExternalConnections
                    .FirstOrDefaultAsync(ec => ec.ParticipantId == participantId && ec.Provider == "Strava");

                if (connection is not null)
                {
                    connection.ConnectionStatus = ConnectionStatus.RequiresReauth;
                    await dbContext.SaveChangesAsync();
                }

                logger.LogWarning("Strava token refresh failed for participant {ParticipantId}: {Error}",
                    participantId, refreshResult.Error);

                return new StravaSyncResultDto { ErrorMessage = "Strava token expired and refresh failed. Please reconnect." };
            }

            tokenData = new StravaTokenData
            {
                AccessToken = refreshResult.AccessToken!,
                RefreshToken = refreshResult.RefreshToken!,
                ExpiresAt = refreshResult.ExpiresAt,
                AthleteId = tokenData.AthleteId
            };

            await stravaTokenService.UpdateTokenDataAsync(participantId, tokenData);
        }

        var result = new StravaSyncResultDto();

        try
        {
            // Get existing external activity ids for this participant to skip duplicates
            var existingExternalIds = await dbContext.Activities
                .AsNoTracking()
                .Where(a => a.ParticipantId == participantId && a.ExternalActivityId != null)
                .Select(a => a.ExternalActivityId!)
                .ToHashSetAsync();

            var allActivities = new List<StravaActivityData>();
            var page = 1;
            const int perPage = 100;

            // Paginate through Strava API
            while (true)
            {
                var activities = await stravaApiClient.GetActivitiesAsync(
                    tokenData.AccessToken,
                    participant.Event.StartDate,
                    participant.Event.EndDate.AddDays(1),
                    page,
                    perPage);

                if (activities.Count == 0)
                    break;

                allActivities.AddRange(activities);
                if (activities.Count < perPage)
                    break;

                page++;
            }

            foreach (var stravaActivity in allActivities)
            {
                var externalId = stravaActivity.Id.ToString();

                // Skip duplicates
                if (existingExternalIds.Contains(externalId))
                {
                    result.SkippedDuplicateCount++;
                    continue;
                }

                // Filter unsupported activity types
                if (!SupportedActivityTypes.Contains(stravaActivity.Type))
                {
                    result.SkippedOutOfRangeCount++;
                    continue;
                }

                // Check activity date is within event range
                if (stravaActivity.StartDateLocal.Date < participant.Event.StartDate.Date
                    || stravaActivity.StartDateLocal.Date > participant.Event.EndDate.Date)
                {
                    result.SkippedOutOfRangeCount++;
                    continue;
                }

                // Convert meters to km
                var distanceKm = Math.Round((decimal)stravaActivity.Distance / 1000m, 2, MidpointRounding.AwayFromZero);

                if (distanceKm <= 0)
                {
                    result.SkippedOutOfRangeCount++;
                    continue;
                }

                var activity = new Activity
                {
                    ParticipantId = participantId,
                    EventId = participant.EventId,
                    ActivityDate = DateTime.SpecifyKind(stravaActivity.StartDateLocal, DateTimeKind.Utc),
                    DistanceKm = distanceKm,
                    RideType = RideType.Other,
                    Source = ActivitySource.Strava,
                    Status = ActivityStatus.Approved,
                    CountsTowardTotal = true,
                    ExternalActivityId = externalId,
                    ExternalTitle = stravaActivity.Name,
                    ImportedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                dbContext.Activities.Add(activity);
                existingExternalIds.Add(externalId);
                result.ImportedCount++;
            }

            await dbContext.SaveChangesAsync();

            if (result.ImportedCount > 0)
            {
                await milestoneCalculationService.RecalculateMilestonesAsync(participant.EventId);
                await badgeService.CheckAndAwardBadgesAsync(participant.EventId, participantId, "strava-sync");
            }

            // Update last sync timestamp
            var conn = await dbContext.ExternalConnections
                .FirstOrDefaultAsync(ec => ec.ParticipantId == participantId && ec.Provider == "Strava");

            if (conn is not null)
            {
                conn.LastSyncAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
            }

            await auditService.LogAsync(
                actor,
                "StravaSync",
                "Participant",
                participantId.ToString(),
                participant.EventId,
                afterSummary: $"Imported: {result.ImportedCount}, Skipped duplicates: {result.SkippedDuplicateCount}, Skipped out-of-range: {result.SkippedOutOfRangeCount}");
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Strava API error during sync for participant {ParticipantId}", participantId);
            result.ErrorMessage = "Failed to fetch activities from Strava. Please try again.";
            result.ErrorCount++;
        }

        return result;
    }

    public async Task<BulkStravaSyncResultDto> SyncAllForEventAsync(Guid eventId, string actor)
    {
        var ev = await dbContext.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event '{eventId}' not found.");

        if (ev.Status != EventStatus.Active)
            throw new InvalidOperationException("Can only sync activities for an active event.");

        if (!ev.StravaEnabled)
            throw new InvalidOperationException("Strava integration is not enabled for this event.");

        // Find all participants with a connected Strava account
        var connectedParticipantIds = await dbContext.ExternalConnections
            .AsNoTracking()
            .Where(ec => ec.Provider == "Strava"
                && ec.ConnectionStatus == ConnectionStatus.Connected
                && ec.Participant.EventId == eventId
                && ec.Participant.Status == ParticipantStatus.Active)
            .Select(ec => new { ec.ParticipantId, ParticipantName = ec.Participant.DisplayName })
            .ToListAsync();

        var bulkResult = new BulkStravaSyncResultDto
        {
            TotalParticipants = connectedParticipantIds.Count
        };

        foreach (var entry in connectedParticipantIds)
        {
            try
            {
                var result = await SyncParticipantAsync(entry.ParticipantId, actor);

                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    bulkResult.FailedSyncs++;
                    bulkResult.Errors.Add($"{entry.ParticipantName}: {result.ErrorMessage}");
                }
                else
                {
                    bulkResult.SuccessfulSyncs++;
                    bulkResult.TotalImported += result.ImportedCount;
                    bulkResult.TotalSkippedDuplicates += result.SkippedDuplicateCount;
                    bulkResult.TotalSkippedOutOfRange += result.SkippedOutOfRangeCount;
                }
            }
            catch (Exception ex)
            {
                bulkResult.FailedSyncs++;
                bulkResult.Errors.Add($"{entry.ParticipantName}: {ex.Message}");
                logger.LogError(ex, "Bulk sync failed for participant {ParticipantId}", entry.ParticipantId);
            }
        }

        await auditService.LogAsync(
            actor,
            "StravaBulkSync",
            "Event",
            eventId.ToString(),
            eventId,
            afterSummary: $"Synced {bulkResult.SuccessfulSyncs}/{bulkResult.TotalParticipants} participants. Imported: {bulkResult.TotalImported}");

        return bulkResult;
    }

    public async Task<ClubActivitySyncResultDto> SyncClubActivitiesAsync(
        Guid eventId, string accessToken, string clubId, string actor)
    {
        var ev = await dbContext.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event '{eventId}' not found.");

        if (ev.Status != EventStatus.Active)
            throw new InvalidOperationException("Can only sync activities for an active event.");

        if (!ev.StravaEnabled)
            throw new InvalidOperationException("Strava integration is not enabled for this event.");

            var result = new ClubActivitySyncResultDto();
            var affectedParticipantIds = new HashSet<Guid>();

        try
        {
            // Fetch club activities from Strava using admin's token
            var clubActivities = await stravaApiClient.GetClubActivitiesAsync(accessToken, clubId);
            result.TotalActivitiesFetched = clubActivities.Count;

            // Load all active participants for this event
            var participants = await dbContext.Participants
                .AsNoTracking()
                .Where(p => p.EventId == eventId && p.Status == ParticipantStatus.Active)
                .Select(p => new { p.Id, p.FirstName, p.LastName })
                .ToListAsync();

            // Build lookup by normalized (firstName, lastName) → participant
            // Group to detect ambiguous name matches
            var nameLookup = participants
                .GroupBy(p => (
                    First: p.FirstName.Trim().ToLowerInvariant(),
                    Last: p.LastName.Trim().ToLowerInvariant()))
                .ToDictionary(
                    g => g.Key,
                    g => g.ToList());

            // Load existing club-synced external IDs for dedup
            var existingExternalIds = await dbContext.Activities
                .AsNoTracking()
                .Where(a => a.EventId == eventId
                    && a.ExternalActivityId != null
                    && a.ExternalActivityId.StartsWith("club:"))
                .Select(a => a.ExternalActivityId!)
                .ToHashSetAsync();

            foreach (var ca in clubActivities)
            {
                // Filter unsupported activity types
                if (!SupportedActivityTypes.Contains(ca.Type))
                {
                    result.SkippedUnsupportedTypeCount++;
                    continue;
                }

                var distanceKm = Math.Round((decimal)ca.Distance / 1000m, 2, MidpointRounding.AwayFromZero);
                if (distanceKm <= 0)
                {
                    result.SkippedUnsupportedTypeCount++;
                    continue;
                }

                // Filter activities outside the event date range
                if (ca.StartDateLocal.HasValue
                    && (ca.StartDateLocal.Value.Date < ev.StartDate.Date
                        || ca.StartDateLocal.Value.Date > ev.EndDate.Date))
                {
                    result.SkippedOutOfRangeCount++;
                    continue;
                }

                // Match athlete to participant by name
                var nameKey = (
                    First: ca.AthleteFirstName.Trim().ToLowerInvariant(),
                    Last: ca.AthleteLastName.Trim().ToLowerInvariant());

                if (!nameLookup.TryGetValue(nameKey, out var matched) || matched.Count == 0)
                {
                    result.UnmatchedCount++;
                    continue;
                }

                if (matched.Count > 1)
                {
                    result.Errors.Add(
                        $"Ambiguous match for {ca.AthleteFirstName} {ca.AthleteLastName}: " +
                        $"{matched.Count} participants share this name. Skipped.");
                    result.UnmatchedCount++;
                    continue;
                }

                var participantId = matched[0].Id;

                // Generate composite external ID for dedup (no activity ID from club API)
                var compositeKey = $"club:{participantId}:{ca.Name}:{distanceKm}:{ca.MovingTime}";

                if (existingExternalIds.Contains(compositeKey))
                {
                    result.SkippedDuplicateCount++;
                    continue;
                }

                var activityDate = ca.StartDateLocal.HasValue
                    ? DateTime.SpecifyKind(ca.StartDateLocal.Value, DateTimeKind.Utc)
                    : DateTime.UtcNow;

                var activity = new Activity
                {
                    ParticipantId = participantId,
                    EventId = eventId,
                    ActivityDate = activityDate,
                    DistanceKm = distanceKm,
                    RideType = RideType.Other,
                    Source = ActivitySource.Strava,
                    Status = ActivityStatus.Approved,
                    CountsTowardTotal = true,
                    ExternalActivityId = compositeKey,
                    ExternalTitle = ca.Name,
                    Notes = "Imported via Strava Club sync",
                    ImportedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                dbContext.Activities.Add(activity);
                existingExternalIds.Add(compositeKey);
                affectedParticipantIds.Add(participantId);
                result.ImportedCount++;
            }

            await dbContext.SaveChangesAsync();

            if (affectedParticipantIds.Count > 0)
            {
                await milestoneCalculationService.RecalculateMilestonesAsync(eventId);
                foreach (var pid in affectedParticipantIds)
                {
                    await badgeService.CheckAndAwardBadgesAsync(eventId, pid, "strava-club-sync");
                }
            }

            await auditService.LogAsync(
                actor,
                "StravaClubSync",
                "Event",
                eventId.ToString(),
                eventId,
                afterSummary: $"Club activities synced. Fetched: {result.TotalActivitiesFetched}, " +
                              $"Imported: {result.ImportedCount}, Duplicates: {result.SkippedDuplicateCount}, " +
                              $"OutOfRange: {result.SkippedOutOfRangeCount}, Unmatched: {result.UnmatchedCount}");
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Strava API error during club activities sync for event {EventId}", eventId);
            result.ErrorMessage = "Failed to fetch club activities from Strava. Please try again.";
        }

        return result;
    }
}
