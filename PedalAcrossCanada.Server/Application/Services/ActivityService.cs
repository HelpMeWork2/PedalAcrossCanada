using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Server.Domain.Entities;
using PedalAcrossCanada.Server.Infrastructure.Data;
using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Activities;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Application.Services;

public class ActivityService(
    AppDbContext dbContext,
    IAuditService auditService,
    IMilestoneCalculationService milestoneCalculationService,
    IBadgeService badgeService) : IActivityService
{
    public async Task<PagedResult<ActivityDto>> GetAllAsync(
        Guid eventId,
        int page,
        int pageSize,
        ActivityStatus? status = null,
        ActivitySource? source = null,
        Guid? participantId = null,
        Guid? teamId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        bool? duplicateFlagged = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.Activities
            .AsNoTracking()
            .Include(a => a.Participant)
            .Where(a => a.EventId == eventId);

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        if (source.HasValue)
            query = query.Where(a => a.Source == source.Value);

        if (participantId.HasValue)
            query = query.Where(a => a.ParticipantId == participantId.Value);

        if (teamId.HasValue)
            query = query.Where(a => a.Participant.TeamId == teamId.Value);

        if (startDate.HasValue)
            query = query.Where(a => a.ActivityDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(a => a.ActivityDate <= endDate.Value);

        if (duplicateFlagged.HasValue)
            query = query.Where(a => a.IsDuplicateFlagged == duplicateFlagged.Value);

        var totalCount = await query.CountAsync();

        var activities = await query
            .OrderByDescending(a => a.ActivityDate)
            .ThenByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => MapToDto(a))
            .ToListAsync();

        return PagedResult<ActivityDto>.Create(activities, page, pageSize, totalCount);
    }

    public async Task<PagedResult<ActivityDto>> GetByParticipantAsync(
        Guid eventId,
        Guid participantId,
        int page,
        int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.Activities
            .AsNoTracking()
            .Include(a => a.Participant)
            .Where(a => a.EventId == eventId && a.ParticipantId == participantId);

        var totalCount = await query.CountAsync();

        var activities = await query
            .OrderByDescending(a => a.ActivityDate)
            .ThenByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => MapToDto(a))
            .ToListAsync();

        return PagedResult<ActivityDto>.Create(activities, page, pageSize, totalCount);
    }

    public async Task<ActivityDto> GetByIdAsync(Guid eventId, Guid activityId)
    {
        var activity = await dbContext.Activities
            .AsNoTracking()
            .Include(a => a.Participant)
            .FirstOrDefaultAsync(a => a.Id == activityId && a.EventId == eventId)
            ?? throw new KeyNotFoundException($"Activity with id '{activityId}' not found.");

        return MapToDto(activity);
    }

    public async Task<CreateActivityResponse> CreateAsync(
        Guid eventId,
        Guid participantId,
        CreateActivityRequest request,
        string actor)
    {
        var ev = await dbContext.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event with id '{eventId}' not found.");

        if (ev.Status == EventStatus.Closed || ev.Status == EventStatus.Archived)
            throw new InvalidOperationException("Cannot create activities for a closed or archived event.");

        if (ev.ManualEntryMode == ManualEntryMode.Disabled)
            throw new InvalidOperationException("Manual entry is disabled for this event.");

        var participant = await dbContext.Participants
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == participantId && p.EventId == eventId)
            ?? throw new KeyNotFoundException($"Participant with id '{participantId}' not found in this event.");

        if (participant.Status != ParticipantStatus.Active)
            throw new InvalidOperationException("Only active participants can create activities.");

        ValidateActivityData(request.DistanceKm, request.ActivityDate, ev);

        var roundedDistance = Math.Round(request.DistanceKm, 2, MidpointRounding.AwayFromZero);

        var initialStatus = ev.ManualEntryMode == ManualEntryMode.AllowedWithApproval
            ? ActivityStatus.Pending
            : ActivityStatus.Approved;

        var activity = new Activity
        {
            ParticipantId = participantId,
            EventId = eventId,
            ActivityDate = request.ActivityDate,
            DistanceKm = roundedDistance,
            RideType = request.RideType,
            Notes = request.Notes,
            Source = ActivitySource.Manual,
            Status = initialStatus,
            CountsTowardTotal = initialStatus == ActivityStatus.Approved,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Activities.Add(activity);
        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "ActivityCreated", "Activity", activity.Id.ToString(),
            eventId, null, JsonSerializer.Serialize(new
            {
                activity.Id,
                activity.ParticipantId,
                activity.ActivityDate,
                activity.DistanceKm,
                activity.RideType,
                activity.Source,
                activity.Status
            }));

        // Check for duplicate candidates: same participant + same date + ±10% distance
        var duplicateCandidate = await FindDuplicateCandidateAsync(
            participantId, activity.Id, request.ActivityDate, roundedDistance);

        // Reload with participant for mapping
        var saved = await dbContext.Activities
            .AsNoTracking()
            .Include(a => a.Participant)
            .FirstAsync(a => a.Id == activity.Id);

        return new CreateActivityResponse
        {
            Activity = MapToDto(saved),
            DuplicateWarning = duplicateCandidate.HasValue,
            CandidateActivityId = duplicateCandidate
        };
    }

    public async Task<ActivityDto> UpdateAsync(
        Guid eventId,
        Guid activityId,
        UpdateActivityRequest request,
        string actor)
    {
        var activity = await GetTrackedActivityAsync(eventId, activityId);

        var ev = await dbContext.Events
            .AsNoTracking()
            .FirstAsync(e => e.Id == eventId);

        if (ev.Status == EventStatus.Closed || ev.Status == EventStatus.Archived)
            throw new InvalidOperationException("Cannot edit activities for a closed or archived event.");

        if (activity.Source != ActivitySource.Manual)
            throw new InvalidOperationException("Only manual activities can be edited.");

        if (activity.LockedByAdmin)
            throw new InvalidOperationException("This activity has been locked by an admin.");

        ValidateActivityData(request.DistanceKm, request.ActivityDate, ev);

        var before = JsonSerializer.Serialize(new
        {
            activity.ActivityDate,
            activity.DistanceKm,
            activity.RideType,
            activity.Notes
        });

        activity.ActivityDate = request.ActivityDate;
        activity.DistanceKm = Math.Round(request.DistanceKm, 2, MidpointRounding.AwayFromZero);
        activity.RideType = request.RideType;
        activity.Notes = request.Notes;
        activity.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "ActivityUpdated", "Activity", activity.Id.ToString(),
            eventId, before, JsonSerializer.Serialize(new
            {
                activity.ActivityDate,
                activity.DistanceKm,
                activity.RideType,
                activity.Notes
            }));

        var updated = await dbContext.Activities
            .AsNoTracking()
            .Include(a => a.Participant)
            .FirstAsync(a => a.Id == activityId);

        return MapToDto(updated);
    }

    public async Task DeleteAsync(Guid eventId, Guid activityId, string actor)
    {
        var activity = await GetTrackedActivityAsync(eventId, activityId);

        var ev = await dbContext.Events
            .AsNoTracking()
            .FirstAsync(e => e.Id == eventId);

        if (ev.Status == EventStatus.Closed || ev.Status == EventStatus.Archived)
            throw new InvalidOperationException("Cannot delete activities for a closed or archived event.");

        if (activity.Source != ActivitySource.Manual)
            throw new InvalidOperationException("Only manual activities can be deleted.");

        if (activity.LockedByAdmin)
            throw new InvalidOperationException("This activity has been locked by an admin.");

        var before = JsonSerializer.Serialize(new
        {
            activity.Id,
            activity.ParticipantId,
            activity.ActivityDate,
            activity.DistanceKm,
            activity.RideType,
            activity.Status
        });

        dbContext.Activities.Remove(activity);
        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "ActivityDeleted", "Activity", activityId.ToString(),
            eventId, before, null);
    }

    public async Task<ActivityDto> ApproveAsync(Guid eventId, Guid activityId, string actor)
    {
        var activity = await GetTrackedActivityAsync(eventId, activityId);

        if (activity.Status != ActivityStatus.Pending)
            throw new InvalidOperationException("Only pending activities can be approved.");

        var before = JsonSerializer.Serialize(new { activity.Status, activity.CountsTowardTotal });

        activity.Status = ActivityStatus.Approved;
        activity.CountsTowardTotal = true;
        activity.ApprovedBy = actor;
        activity.ApprovedAt = DateTime.UtcNow;
        activity.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        await milestoneCalculationService.RecalculateMilestonesAsync(activity.EventId);
        await badgeService.CheckAndAwardBadgesAsync(activity.EventId, activity.ParticipantId, actor);

        await auditService.LogAsync(
            actor, "ActivityApproved", "Activity", activityId.ToString(),
            eventId, before, JsonSerializer.Serialize(new
            {
                activity.Status,
                activity.CountsTowardTotal,
                activity.ApprovedBy,
                activity.ApprovedAt
            }));

        var updated = await dbContext.Activities
            .AsNoTracking()
            .Include(a => a.Participant)
            .FirstAsync(a => a.Id == activityId);

        return MapToDto(updated);
    }

    public async Task<ActivityDto> RejectAsync(
        Guid eventId,
        Guid activityId,
        RejectActivityRequest request,
        string actor)
    {
        var activity = await GetTrackedActivityAsync(eventId, activityId);

        if (activity.Status != ActivityStatus.Pending)
            throw new InvalidOperationException("Only pending activities can be rejected.");

        var before = JsonSerializer.Serialize(new { activity.Status, activity.CountsTowardTotal });

        activity.Status = ActivityStatus.Rejected;
        activity.CountsTowardTotal = false;
        activity.RejectedBy = actor;
        activity.RejectedAt = DateTime.UtcNow;
        activity.RejectionReason = request.Reason;
        activity.UpdatedAt = DateTime.UtcNow;

        // Create a notification for the participant
        var notification = new Notification
        {
            ParticipantId = activity.ParticipantId,
            NotificationType = NotificationType.ActivityRejected,
            Title = "Activity Rejected",
            Message = $"Your activity on {activity.ActivityDate:MMM d, yyyy} ({activity.DistanceKm:N2} km) was rejected. Reason: {request.Reason}",
            RelatedEntityType = "Activity",
            RelatedEntityId = activityId.ToString(),
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "ActivityRejected", "Activity", activityId.ToString(),
            eventId, before, JsonSerializer.Serialize(new
            {
                activity.Status,
                activity.RejectedBy,
                activity.RejectedAt,
                activity.RejectionReason
            }));

        var updated = await dbContext.Activities
            .AsNoTracking()
            .Include(a => a.Participant)
            .FirstAsync(a => a.Id == activityId);

        return MapToDto(updated);
    }

    public async Task<ActivityDto> InvalidateAsync(
        Guid eventId,
        Guid activityId,
        InvalidateActivityRequest request,
        string actor)
    {
        var activity = await GetTrackedActivityAsync(eventId, activityId);

        if (activity.Status == ActivityStatus.Invalid)
            throw new InvalidOperationException("Activity is already invalid.");

        var before = JsonSerializer.Serialize(new { activity.Status, activity.CountsTowardTotal });

        activity.Status = ActivityStatus.Invalid;
        activity.CountsTowardTotal = false;
        activity.RejectionReason = request.Reason;
        activity.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "ActivityInvalidated", "Activity", activityId.ToString(),
            eventId, before, JsonSerializer.Serialize(new
            {
                activity.Status,
                activity.CountsTowardTotal,
                Reason = request.Reason
            }));

        var updated = await dbContext.Activities
            .AsNoTracking()
            .Include(a => a.Participant)
            .FirstAsync(a => a.Id == activityId);

        return MapToDto(updated);
    }

    public async Task<ActivityDto> LockAsync(Guid eventId, Guid activityId, string actor)
    {
        var activity = await GetTrackedActivityAsync(eventId, activityId);

        activity.LockedByAdmin = true;
        activity.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "ActivityLocked", "Activity", activityId.ToString(),
            eventId, null, JsonSerializer.Serialize(new { activity.LockedByAdmin }));

        var updated = await dbContext.Activities
            .AsNoTracking()
            .Include(a => a.Participant)
            .FirstAsync(a => a.Id == activityId);

        return MapToDto(updated);
    }

    private async Task<Activity> GetTrackedActivityAsync(Guid eventId, Guid activityId)
    {
        return await dbContext.Activities
            .FirstOrDefaultAsync(a => a.Id == activityId && a.EventId == eventId)
            ?? throw new KeyNotFoundException($"Activity with id '{activityId}' not found.");
    }

    private static void ValidateActivityData(decimal distanceKm, DateTime activityDate, Event ev)
    {
        if (distanceKm <= 0)
            throw new ArgumentException("Distance must be greater than 0.");

        if (distanceKm > ev.MaxSingleRideKm)
            throw new ArgumentException($"Distance exceeds the maximum allowed per ride ({ev.MaxSingleRideKm:N2} km).");

        if (activityDate > DateTime.UtcNow)
            throw new ArgumentException("Activity date cannot be in the future.");

        if (activityDate.Date < ev.StartDate.Date || activityDate.Date > ev.EndDate.Date)
            throw new ArgumentException("Activity date must be within the event date range.");
    }

    private async Task<Guid?> FindDuplicateCandidateAsync(
        Guid participantId,
        Guid excludeActivityId,
        DateTime activityDate,
        decimal distanceKm)
    {
        var lowerBound = distanceKm * 0.9m;
        var upperBound = distanceKm * 1.1m;

        var candidate = await dbContext.Activities
            .AsNoTracking()
            .Where(a =>
                a.ParticipantId == participantId &&
                a.Id != excludeActivityId &&
                a.ActivityDate.Date == activityDate.Date &&
                a.DistanceKm >= lowerBound &&
                a.DistanceKm <= upperBound)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync();

        return candidate;
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
        LockedByAdmin = activity.LockedByAdmin,
        CreatedAt = activity.CreatedAt,
        UpdatedAt = activity.UpdatedAt
    };
}
