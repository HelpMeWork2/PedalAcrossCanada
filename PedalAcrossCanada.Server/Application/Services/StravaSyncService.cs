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
}
