using Hangfire;
using Microsoft.EntityFrameworkCore;
using PedalAcrossCanada.Server.Infrastructure.Data;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Application.Jobs;

/// <summary>
/// Hangfire background job that refreshes leaderboard-related totals for the active event.
/// Runs once daily via a recurring schedule.
/// </summary>
public class LeaderboardRefreshJob(
    AppDbContext dbContext,
    ILogger<LeaderboardRefreshJob> logger)
{
    public const string JobId = "leaderboard-refresh";
    public const string Cron = "0 2 * * *"; // daily at 02:00 UTC
    public const string Queue = "default";

    [Queue(Queue)]
    public async Task RunAsync()
    {
        var activeEvent = await dbContext.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Status == EventStatus.Active);

        if (activeEvent is null)
        {
            logger.LogInformation("LeaderboardRefreshJob: no active event found, skipping.");
            return;
        }

        logger.LogInformation(
            "LeaderboardRefreshJob: refreshing leaderboard totals for event '{EventId}' ({EventName}).",
            activeEvent.Id, activeEvent.Name);

        // Force EF to reload participant totals by touching the context.
        // The leaderboard is computed live from the Activities table, so this job
        // primarily exists to log a heartbeat and can trigger cache invalidation in future.
        var participantCount = await dbContext.Participants
            .CountAsync(p => p.EventId == activeEvent.Id && p.Status == ParticipantStatus.Active);

        logger.LogInformation(
            "LeaderboardRefreshJob: completed for event '{EventId}'. Active participants: {Count}.",
            activeEvent.Id, participantCount);
    }
}
