using Microsoft.EntityFrameworkCore;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Server.Domain.Entities;
using PedalAcrossCanada.Server.Infrastructure.Data;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Application.Services;

public class MilestoneCalculationService(AppDbContext dbContext, IAuditService auditService)
    : IMilestoneCalculationService
{
    public async Task RecalculateMilestonesAsync(Guid eventId)
    {
        var totalEventKm = await dbContext.Activities
            .Where(a => a.EventId == eventId
                        && a.Status == ActivityStatus.Approved
                        && a.CountsTowardTotal)
            .SumAsync(a => a.DistanceKm);

        var milestones = await dbContext.Milestones
            .Where(m => m.EventId == eventId)
            .OrderBy(m => m.CumulativeDistanceKm)
            .ToListAsync();

        foreach (var milestone in milestones)
        {
            if (milestone.AchievedAt.HasValue)
            {
                // Already achieved — skip (idempotent)
                continue;
            }

            if (totalEventKm >= milestone.CumulativeDistanceKm)
            {
                milestone.AchievedAt = DateTime.UtcNow;
                milestone.TotalKmAtAchievement = totalEventKm;

                await auditService.LogAsync(
                    "system", "MilestoneAchieved", "Milestone", milestone.Id.ToString(),
                    eventId, null,
                    $"{{\"CumulativeDistanceKm\":{milestone.CumulativeDistanceKm},\"TotalKmAtAchievement\":{totalEventKm}}}");

                // Create milestone notifications for all active participants
                var activeParticipantIds = await dbContext.Participants
                    .AsNoTracking()
                    .Where(p => p.EventId == eventId && p.Status == ParticipantStatus.Active)
                    .Select(p => p.Id)
                    .ToListAsync();

                foreach (var participantId in activeParticipantIds)
                {
                    dbContext.Notifications.Add(new Notification
                    {
                        ParticipantId = participantId,
                        NotificationType = NotificationType.MilestoneReached,
                        Title = "Milestone Reached!",
                        Message = $"The team has reached {milestone.StopName} ({milestone.CumulativeDistanceKm:N2} km)!",
                        RelatedEntityType = "Milestone",
                        RelatedEntityId = milestone.Id.ToString(),
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        await dbContext.SaveChangesAsync();
    }
}
