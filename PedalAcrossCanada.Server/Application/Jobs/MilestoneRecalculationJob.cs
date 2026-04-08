using Hangfire;
using Microsoft.EntityFrameworkCore;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Server.Infrastructure.Data;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Application.Jobs;

/// <summary>
/// Hangfire background job that recalculates milestone achievements for the active event.
/// Runs every hour via a recurring schedule to catch any gaps from activity approvals.
/// </summary>
public class MilestoneRecalculationJob(
    AppDbContext dbContext,
    IMilestoneCalculationService milestoneCalculationService,
    ILogger<MilestoneRecalculationJob> logger)
{
    public const string JobId = "milestone-recalculation";
    public const string Cron = "0 * * * *"; // every hour
    public const string Queue = "default";

    [Queue(Queue)]
    public async Task RunAsync()
    {
        var activeEvent = await dbContext.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Status == EventStatus.Active);

        if (activeEvent is null)
        {
            logger.LogInformation("MilestoneRecalculationJob: no active event found, skipping.");
            return;
        }

        logger.LogInformation(
            "MilestoneRecalculationJob: recalculating milestones for event '{EventId}' ({EventName}).",
            activeEvent.Id, activeEvent.Name);

        try
        {
            await milestoneCalculationService.RecalculateMilestonesAsync(activeEvent.Id);

            logger.LogInformation(
                "MilestoneRecalculationJob: completed for event '{EventId}'.",
                activeEvent.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "MilestoneRecalculationJob: failed for event '{EventId}'.",
                activeEvent.Id);
            throw;
        }
    }
}
