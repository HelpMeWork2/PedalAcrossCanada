using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Server.Domain.Entities;
using PedalAcrossCanada.Server.Infrastructure.Data;
using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Activities;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Application.Services;

public class DuplicateService(
    AppDbContext dbContext,
    IAuditService auditService,
    IMilestoneCalculationService milestoneCalculationService,
    IBadgeService badgeService) : IDuplicateService
{
    public async Task FlagPairAsync(Guid firstActivityId, Guid secondActivityId, string actor)
    {
        var first = await dbContext.Activities
            .Include(a => a.Participant)
            .FirstOrDefaultAsync(a => a.Id == firstActivityId)
            ?? throw new KeyNotFoundException($"Activity '{firstActivityId}' not found.");

        var second = await dbContext.Activities
            .Include(a => a.Participant)
            .FirstOrDefaultAsync(a => a.Id == secondActivityId)
            ?? throw new KeyNotFoundException($"Activity '{secondActivityId}' not found.");

        // Idempotent: second already points at first
        if (second.IsDuplicateFlagged && second.DuplicateOfActivityId == firstActivityId)
            return;

        // Only the "second" activity holds the FK pointer; both are flagged
        first.IsDuplicateFlagged = true;
        first.UpdatedAt = DateTime.UtcNow;

        second.IsDuplicateFlagged = true;
        second.DuplicateOfActivityId = firstActivityId;
        second.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "DuplicateFlagged", "Activity", firstActivityId.ToString(),
            first.EventId,
            null,
            JsonSerializer.Serialize(new { FirstActivityId = firstActivityId, SecondActivityId = secondActivityId }));
    }

    public async Task<PagedResult<DuplicatePairDto>> GetFlaggedPairsAsync(Guid eventId, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // Each pair is represented once: second.DuplicateOfActivityId points to first.
        // We query the "second" (the later-created one that has a non-null DuplicateOfActivityId)
        // and always join back to the "first" via the FK.
        var query = dbContext.Activities
            .AsNoTracking()
            .Include(a => a.Participant)
            .Where(a =>
                a.EventId == eventId &&
                a.IsDuplicateFlagged &&
                a.DuplicateOfActivityId != null);

        var totalCount = await query.CountAsync();

        var seconds = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var firstIds = seconds
            .Select(a => a.DuplicateOfActivityId!.Value)
            .Distinct()
            .ToList();

        var firstActivities = await dbContext.Activities
            .AsNoTracking()
            .Include(a => a.Participant)
            .Where(a => firstIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        var pairs = seconds
            .Where(s => firstActivities.ContainsKey(s.DuplicateOfActivityId!.Value))
            .Select(s => new DuplicatePairDto
            {
                First = MapToDto(firstActivities[s.DuplicateOfActivityId!.Value]),
                Second = MapToDto(s)
            })
            .ToList();

        return PagedResult<DuplicatePairDto>.Create(pairs, page, pageSize, totalCount);
    }

    public async Task<DuplicatePairDto> ResolveAsync(
        Guid eventId,
        Guid activityId,
        DuplicateResolution resolution,
        string actor)
    {
        // Find the "second" activity (it holds the DuplicateOfActivityId pointer)
        var second = await dbContext.Activities
            .Include(a => a.Participant)
            .FirstOrDefaultAsync(a => a.Id == activityId && a.EventId == eventId)
            ?? throw new KeyNotFoundException($"Activity '{activityId}' not found in event '{eventId}'.");

        if (!second.IsDuplicateFlagged || second.DuplicateOfActivityId == null)
            throw new InvalidOperationException("Activity is not flagged as a duplicate.");

        var first = await dbContext.Activities
            .Include(a => a.Participant)
            .FirstOrDefaultAsync(a => a.Id == second.DuplicateOfActivityId.Value)
            ?? throw new KeyNotFoundException($"Paired activity '{second.DuplicateOfActivityId}' not found.");

        var before = JsonSerializer.Serialize(new
        {
            FirstStatus = first.Status,
            FirstCountsTowardTotal = first.CountsTowardTotal,
            SecondStatus = second.Status,
            SecondCountsTowardTotal = second.CountsTowardTotal
        });

        switch (resolution)
        {
            case DuplicateResolution.KeepBoth:
                ClearFlags(first, second);
                break;

            case DuplicateResolution.KeepFirst:
                Invalidate(second, "Duplicate of activity " + first.Id);
                ClearFlags(first, second);
                break;

            case DuplicateResolution.KeepSecond:
                Invalidate(first, "Duplicate of activity " + second.Id);
                ClearFlags(first, second);
                break;
        }

        await dbContext.SaveChangesAsync();

        // Recalculate totals if an activity was invalidated
        if (resolution != DuplicateResolution.KeepBoth)
        {
            await milestoneCalculationService.RecalculateMilestonesAsync(eventId);

            var participantToRecalculate = resolution == DuplicateResolution.KeepFirst
                ? second.ParticipantId
                : first.ParticipantId;

            await badgeService.CheckAndAwardBadgesAsync(eventId, participantToRecalculate, actor);
        }

        var afterFirst = await dbContext.Activities.AsNoTracking()
            .Include(a => a.Participant)
            .FirstAsync(a => a.Id == first.Id);

        var afterSecond = await dbContext.Activities.AsNoTracking()
            .Include(a => a.Participant)
            .FirstAsync(a => a.Id == second.Id);

        await auditService.LogAsync(
            actor, "DuplicateResolved", "Activity", activityId.ToString(),
            eventId,
            before,
            JsonSerializer.Serialize(new
            {
                Resolution = resolution.ToString(),
                FirstActivityId = first.Id,
                SecondActivityId = second.Id,
                FirstStatus = afterFirst.Status.ToString(),
                SecondStatus = afterSecond.Status.ToString()
            }));

        return new DuplicatePairDto
        {
            First = MapToDto(afterFirst),
            Second = MapToDto(afterSecond)
        };
    }

    private static void ClearFlags(Activity first, Activity second)
    {
        first.IsDuplicateFlagged = false;
        first.DuplicateOfActivityId = null;
        first.UpdatedAt = DateTime.UtcNow;

        second.IsDuplicateFlagged = false;
        second.DuplicateOfActivityId = null;
        second.UpdatedAt = DateTime.UtcNow;
    }

    private static void Invalidate(Activity activity, string reason)
    {
        activity.Status = ActivityStatus.Invalid;
        activity.CountsTowardTotal = false;
        activity.RejectionReason = reason;
        activity.UpdatedAt = DateTime.UtcNow;
    }

    private static ActivityDto MapToDto(Activity activity) => new()
    {
        Id = activity.Id,
        ParticipantId = activity.ParticipantId,
        EventId = activity.EventId,
        ParticipantDisplayName = activity.Participant?.DisplayName ?? string.Empty,
        ActivityDate = activity.ActivityDate,
        DistanceKm = activity.DistanceKm,
        RideType = activity.RideType,
        Notes = activity.Notes,
        Source = activity.Source,
        Status = activity.Status,
        CountsTowardTotal = activity.CountsTowardTotal,
        ExternalActivityId = activity.ExternalActivityId,
        ExternalTitle = activity.ExternalTitle,
        ImportedAt = activity.ImportedAt,
        ApprovedBy = activity.ApprovedBy,
        ApprovedAt = activity.ApprovedAt,
        RejectionReason = activity.RejectionReason,
        IsDuplicateFlagged = activity.IsDuplicateFlagged,
        DuplicateOfActivityId = activity.DuplicateOfActivityId,
        LockedByAdmin = activity.LockedByAdmin,
        CreatedAt = activity.CreatedAt,
        UpdatedAt = activity.UpdatedAt
    };
}
