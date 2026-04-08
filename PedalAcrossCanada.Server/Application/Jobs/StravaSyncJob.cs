using Hangfire;
using Microsoft.EntityFrameworkCore;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Server.Infrastructure.Data;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Application.Jobs;

/// <summary>
/// Hangfire background job that syncs Strava activities for all connected participants
/// in the currently active event. Runs every 30 minutes via a recurring schedule.
/// </summary>
public class StravaSyncJob(
    AppDbContext dbContext,
    IStravaSyncService stravaSyncService,
    ILogger<StravaSyncJob> logger)
{
    public const string JobId = "strava-sync-all";
    public const string Cron = "*/30 * * * *";
    public const string Queue = "strava";

    [Queue(Queue)]
    public async Task RunAsync()
    {
        var activeEvent = await dbContext.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Status == EventStatus.Active);

        if (activeEvent is null)
        {
            logger.LogInformation("StravaSyncJob: no active event found, skipping.");
            return;
        }

        if (!activeEvent.StravaEnabled)
        {
            logger.LogInformation("StravaSyncJob: Strava not enabled for event '{EventId}', skipping.", activeEvent.Id);
            return;
        }

        logger.LogInformation("StravaSyncJob: starting sync for event '{EventId}' ({EventName}).",
            activeEvent.Id, activeEvent.Name);

        try
        {
            var result = await stravaSyncService.SyncAllForEventAsync(activeEvent.Id, "system");

            logger.LogInformation(
                "StravaSyncJob: completed. Participants: {Total}, Success: {Success}, Failed: {Failed}, Imported: {Imported}.",
                result.TotalParticipants,
                result.SuccessfulSyncs,
                result.FailedSyncs,
                result.TotalImported);

            if (result.Errors.Count > 0)
            {
                foreach (var error in result.Errors)
                    logger.LogWarning("StravaSyncJob participant error: {Error}", error);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "StravaSyncJob: unhandled error during sync for event '{EventId}'.", activeEvent.Id);
            throw; // Re-throw so Hangfire marks the job as failed and can retry
        }
    }
}
