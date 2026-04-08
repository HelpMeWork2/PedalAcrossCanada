using Microsoft.EntityFrameworkCore;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Server.Infrastructure.Data;
using PedalAcrossCanada.Shared.DTOs.Activities;
using PedalAcrossCanada.Shared.DTOs.Dashboards;
using PedalAcrossCanada.Shared.DTOs.Milestones;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Application.Services;

public class DashboardService(
    AppDbContext dbContext,
    ILeaderboardService leaderboardService) : IDashboardService
{
    public async Task<EventDashboardDto> GetEventDashboardAsync(Guid eventId)
    {
        var ev = await dbContext.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event with id '{eventId}' not found.");

        var totalEventKm = await dbContext.Activities
            .Where(a => a.EventId == eventId
                        && a.Status == ActivityStatus.Approved
                        && a.CountsTowardTotal)
            .SumAsync(a => a.DistanceKm);

        var registeredParticipants = await dbContext.Participants
            .CountAsync(p => p.EventId == eventId);

        var activeParticipants = await dbContext.Participants
            .CountAsync(p => p.EventId == eventId && p.Status == ParticipantStatus.Active);

        var totalActivities = await dbContext.Activities
            .CountAsync(a => a.EventId == eventId
                             && a.Status == ActivityStatus.Approved
                             && a.CountsTowardTotal);

        var milestones = await dbContext.Milestones
            .AsNoTracking()
            .Where(m => m.EventId == eventId)
            .OrderBy(m => m.CumulativeDistanceKm)
            .ToListAsync();

        var completedMilestones = milestones
            .Where(m => m.AchievedAt.HasValue)
            .Select(MapMilestoneToDto)
            .ToList();

        var nextMilestone = milestones
            .Where(m => !m.AchievedAt.HasValue)
            .OrderBy(m => m.CumulativeDistanceKm)
            .Select(MapMilestoneToDto)
            .FirstOrDefault();

        var percentComplete = ev.RouteDistanceKm > 0
            ? Math.Round(totalEventKm / ev.RouteDistanceKm * 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;

        // Determine current virtual location based on achieved milestones
        var currentLocation = milestones
            .Where(m => m.AchievedAt.HasValue)
            .OrderByDescending(m => m.CumulativeDistanceKm)
            .Select(m => m.StopName)
            .FirstOrDefault();

        return new EventDashboardDto
        {
            TotalEventKm = totalEventKm,
            RouteDistanceKm = ev.RouteDistanceKm,
            PercentComplete = Math.Min(percentComplete, 100m),
            TimesAroundRoute = ev.RouteDistanceKm > 0
                ? (int)Math.Floor(totalEventKm / ev.RouteDistanceKm)
                : 0,
            NearestCity = currentLocation,
            RegisteredParticipants = registeredParticipants,
            ActiveParticipants = activeParticipants,
            TotalActivities = totalActivities,
            CompletedMilestones = completedMilestones,
            NextMilestone = nextMilestone
        };
    }

    public async Task<ParticipantDashboardDto> GetParticipantDashboardAsync(Guid eventId, Guid participantId)
    {
        var participant = await dbContext.Participants
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == participantId && p.EventId == eventId)
            ?? throw new KeyNotFoundException($"Participant with id '{participantId}' not found in this event.");

        var ev = await dbContext.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event with id '{eventId}' not found.");

        var personalTotalKm = await dbContext.Activities
            .Where(a => a.ParticipantId == participantId
                        && a.EventId == eventId
                        && a.Status == ActivityStatus.Approved
                        && a.CountsTowardTotal)
            .SumAsync(a => a.DistanceKm);

        var personalRideCount = await dbContext.Activities
            .CountAsync(a => a.ParticipantId == participantId
                             && a.EventId == eventId
                             && a.Status == ActivityStatus.Approved
                             && a.CountsTowardTotal);

        var teamTotalKm = 0m;
        int? teamRank = null;

        if (participant.TeamId.HasValue)
        {
            teamTotalKm = await dbContext.Activities
                .Where(a => a.EventId == eventId
                            && a.Status == ActivityStatus.Approved
                            && a.CountsTowardTotal
                            && a.Participant.TeamId == participant.TeamId)
                .SumAsync(a => a.DistanceKm);

            teamRank = await leaderboardService.GetTeamRankAsync(eventId, participant.TeamId.Value);
        }

        var eventTotalKm = await dbContext.Activities
            .Where(a => a.EventId == eventId
                        && a.Status == ActivityStatus.Approved
                        && a.CountsTowardTotal)
            .SumAsync(a => a.DistanceKm);

        var personalRank = await leaderboardService.GetParticipantRankAsync(eventId, participantId);

        var percentComplete = ev.RouteDistanceKm > 0
            ? Math.Round(eventTotalKm / ev.RouteDistanceKm * 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;

        // Next milestone
        var nextMilestone = await dbContext.Milestones
            .AsNoTracking()
            .Where(m => m.EventId == eventId && !m.AchievedAt.HasValue)
            .OrderBy(m => m.CumulativeDistanceKm)
            .FirstOrDefaultAsync();

        // Recent activities (last 5)
        var recentActivities = await dbContext.Activities
            .AsNoTracking()
            .Include(a => a.Participant)
            .Where(a => a.ParticipantId == participantId && a.EventId == eventId)
            .OrderByDescending(a => a.ActivityDate)
            .ThenByDescending(a => a.CreatedAt)
            .Take(5)
            .Select(a => new ActivityDto
            {
                Id = a.Id,
                ParticipantId = a.ParticipantId,
                EventId = a.EventId,
                ParticipantDisplayName = a.Participant.DisplayName,
                ActivityDate = a.ActivityDate,
                DistanceKm = a.DistanceKm,
                RideType = a.RideType,
                Notes = a.Notes,
                Source = a.Source,
                Status = a.Status,
                CountsTowardTotal = a.CountsTowardTotal,
                ExternalActivityId = a.ExternalActivityId,
                ExternalTitle = a.ExternalTitle,
                ImportedAt = a.ImportedAt,
                ApprovedBy = a.ApprovedBy,
                ApprovedAt = a.ApprovedAt,
                RejectionReason = a.RejectionReason,
                IsDuplicateFlagged = a.IsDuplicateFlagged,
                LockedByAdmin = a.LockedByAdmin,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt
            })
            .ToListAsync();

        return new ParticipantDashboardDto
        {
            PersonalTotalKm = personalTotalKm,
            PersonalRideCount = personalRideCount,
            PersonalRank = personalRank,
            TeamTotalKm = teamTotalKm,
            TeamRank = teamRank,
            EventTotalKm = eventTotalKm,
            RouteDistanceKm = ev.RouteDistanceKm,
            PercentComplete = Math.Min(percentComplete, 100m),
            NextMilestoneName = nextMilestone?.StopName,
            KmToNextMilestone = nextMilestone is not null
                ? Math.Max(0, nextMilestone.CumulativeDistanceKm - eventTotalKm)
                : null,
            RecentActivities = recentActivities
        };
    }

    public async Task<AdminDashboardDto> GetAdminDashboardAsync(Guid eventId)
    {
        var ev = await dbContext.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event with id '{eventId}' not found.");

        var totalEventKm = await dbContext.Activities
            .Where(a => a.EventId == eventId
                        && a.Status == ActivityStatus.Approved
                        && a.CountsTowardTotal)
            .SumAsync(a => a.DistanceKm);

        var registeredParticipants = await dbContext.Participants
            .CountAsync(p => p.EventId == eventId);

        var activeParticipants = await dbContext.Participants
            .CountAsync(p => p.EventId == eventId && p.Status == ParticipantStatus.Active);

        var totalActivities = await dbContext.Activities
            .CountAsync(a => a.EventId == eventId
                             && a.Status == ActivityStatus.Approved
                             && a.CountsTowardTotal);

        var pendingApprovals = await dbContext.Activities
            .CountAsync(a => a.EventId == eventId && a.Status == ActivityStatus.Pending);

        var duplicateFlags = await dbContext.Activities
            .CountAsync(a => a.EventId == eventId && a.IsDuplicateFlagged);

        var syncFailures = await dbContext.Notifications
            .CountAsync(n => n.NotificationType == NotificationType.StravaSyncFailed
                             && n.Participant != null
                             && n.Participant.EventId == eventId);

        var percentComplete = ev.RouteDistanceKm > 0
            ? Math.Round(totalEventKm / ev.RouteDistanceKm * 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;

        return new AdminDashboardDto
        {
            TotalEventKm = totalEventKm,
            RouteDistanceKm = ev.RouteDistanceKm,
            PercentComplete = Math.Min(percentComplete, 100m),
            RegisteredParticipants = registeredParticipants,
            ActiveParticipants = activeParticipants,
            TotalActivities = totalActivities,
            PendingApprovals = pendingApprovals,
            DuplicateFlags = duplicateFlags,
            SyncFailures = syncFailures
        };
    }

    private static MilestoneDto MapMilestoneToDto(Domain.Entities.Milestone milestone) => new()
    {
        Id = milestone.Id,
        EventId = milestone.EventId,
        StopName = milestone.StopName,
        OrderIndex = milestone.OrderIndex,
        CumulativeDistanceKm = milestone.CumulativeDistanceKm,
        Description = milestone.Description,
        RewardText = milestone.RewardText,
        AchievedAt = milestone.AchievedAt,
        TotalKmAtAchievement = milestone.TotalKmAtAchievement,
        AnnouncementStatus = milestone.AnnouncementStatus,
        AnnouncedBy = milestone.AnnouncedBy,
        AnnouncedAt = milestone.AnnouncedAt
    };
}
