using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Server.Infrastructure.Data;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Application.Services;

public class ReportService(AppDbContext dbContext) : IReportService
{
    private static readonly CsvConfiguration CsvConfig = new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true
    };

    public async Task<byte[]> GetParticipantsReportAsync(
        Guid eventId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        Guid? teamId = null)
    {
        var query = dbContext.Participants
            .AsNoTracking()
            .Where(p => p.EventId == eventId);

        if (teamId.HasValue)
            query = query.Where(p => p.TeamId == teamId.Value);

        if (startDate.HasValue)
            query = query.Where(p => p.JoinedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(p => p.JoinedAt <= endDate.Value);

        var rows = await query
            .OrderBy(p => p.DisplayName)
            .Select(p => new ParticipantReportRow
            {
                DisplayName = p.DisplayName,
                FirstName = p.FirstName,
                LastName = p.LastName,
                WorkEmail = p.WorkEmail,
                TeamName = p.Team != null ? p.Team.Name : string.Empty,
                Status = p.Status.ToString(),
                JoinedAt = p.JoinedAt,
                TotalKm = p.Activities
                    .Where(a => a.Status == ActivityStatus.Approved && a.CountsTowardTotal)
                    .Sum(a => a.DistanceKm),
                RideCount = p.Activities
                    .Count(a => a.Status == ActivityStatus.Approved && a.CountsTowardTotal),
                GeneratedAt = DateTime.UtcNow
            })
            .ToListAsync();

        return WriteCsv(rows);
    }

    public async Task<byte[]> GetActivitiesReportAsync(
        Guid eventId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        Guid? teamId = null,
        Guid? participantId = null)
    {
        var query = dbContext.Activities
            .AsNoTracking()
            .Where(a => a.EventId == eventId);

        if (startDate.HasValue)
            query = query.Where(a => a.ActivityDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(a => a.ActivityDate <= endDate.Value);

        if (participantId.HasValue)
            query = query.Where(a => a.ParticipantId == participantId.Value);

        if (teamId.HasValue)
            query = query.Where(a => a.Participant.TeamId == teamId.Value);

        var rows = await query
            .OrderByDescending(a => a.ActivityDate)
            .Select(a => new ActivityReportRow
            {
                ParticipantDisplayName = a.Participant.DisplayName,
                TeamName = a.Participant.Team != null ? a.Participant.Team.Name : string.Empty,
                ActivityDate = a.ActivityDate,
                DistanceKm = a.DistanceKm,
                RideType = a.RideType.ToString(),
                Source = a.Source.ToString(),
                Status = a.Status.ToString(),
                CountsTowardTotal = a.CountsTowardTotal,
                Notes = a.Notes ?? string.Empty,
                ApprovedBy = a.ApprovedBy ?? string.Empty,
                ApprovedAt = a.ApprovedAt,
                RejectionReason = a.RejectionReason ?? string.Empty,
                GeneratedAt = DateTime.UtcNow
            })
            .ToListAsync();

        return WriteCsv(rows);
    }

    public async Task<byte[]> GetTeamTotalsReportAsync(
        Guid eventId,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var rows = await dbContext.Teams
            .AsNoTracking()
            .Where(t => t.EventId == eventId)
            .Select(t => new TeamTotalsReportRow
            {
                TeamName = t.Name,
                ActiveParticipantCount = t.Participants.Count(p => p.Status == ParticipantStatus.Active),
                TotalParticipantCount = t.Participants.Count,
                TotalKm = t.Participants
                    .SelectMany(p => p.Activities)
                    .Where(a => a.Status == ActivityStatus.Approved
                                && a.CountsTowardTotal
                                && (!startDate.HasValue || a.ActivityDate >= startDate.Value)
                                && (!endDate.HasValue || a.ActivityDate <= endDate.Value))
                    .Sum(a => a.DistanceKm),
                AverageKmPerActiveParticipant = t.Participants
                    .Where(p => p.Status == ParticipantStatus.Active)
                    .Count() == 0
                    ? 0m
                    : t.Participants
                        .Where(p => p.Status == ParticipantStatus.Active)
                        .SelectMany(p => p.Activities)
                        .Where(a => a.Status == ActivityStatus.Approved
                                    && a.CountsTowardTotal
                                    && (!startDate.HasValue || a.ActivityDate >= startDate.Value)
                                    && (!endDate.HasValue || a.ActivityDate <= endDate.Value))
                        .Sum(a => a.DistanceKm)
                    / t.Participants.Count(p => p.Status == ParticipantStatus.Active),
                GeneratedAt = DateTime.UtcNow
            })
            .OrderByDescending(r => r.TotalKm)
            .ToListAsync();

        return WriteCsv(rows);
    }

    public async Task<byte[]> GetMilestonesReportAsync(Guid eventId)
    {
        var rows = await dbContext.Milestones
            .AsNoTracking()
            .Where(m => m.EventId == eventId)
            .OrderBy(m => m.CumulativeDistanceKm)
            .Select(m => new MilestoneReportRow
            {
                StopName = m.StopName,
                OrderIndex = m.OrderIndex,
                CumulativeDistanceKm = m.CumulativeDistanceKm,
                Description = m.Description ?? string.Empty,
                RewardText = m.RewardText ?? string.Empty,
                IsAchieved = m.AchievedAt.HasValue,
                AchievedAt = m.AchievedAt,
                TotalKmAtAchievement = m.TotalKmAtAchievement,
                AnnouncementStatus = m.AnnouncementStatus.ToString(),
                GeneratedAt = DateTime.UtcNow
            })
            .ToListAsync();

        return WriteCsv(rows);
    }

    public async Task<byte[]> GetBadgeAwardsReportAsync(
        Guid eventId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        Guid? teamId = null,
        Guid? participantId = null)
    {
        var query = dbContext.BadgeAwards
            .AsNoTracking()
            .Where(ba => ba.EventId == eventId);

        if (startDate.HasValue)
            query = query.Where(ba => ba.AwardedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(ba => ba.AwardedAt <= endDate.Value);

        if (participantId.HasValue)
            query = query.Where(ba => ba.ParticipantId == participantId.Value);

        if (teamId.HasValue)
            query = query.Where(ba => ba.Participant.TeamId == teamId.Value);

        var rows = await query
            .OrderByDescending(ba => ba.AwardedAt)
            .Select(ba => new BadgeAwardReportRow
            {
                BadgeName = ba.Badge.Name,
                ThresholdKm = ba.Badge.ThresholdKm ?? 0m,
                ParticipantDisplayName = ba.Participant.DisplayName,
                TeamName = ba.Participant.Team != null ? ba.Participant.Team.Name : string.Empty,
                AwardedAt = ba.AwardedAt,
                IsManual = ba.IsManual,
                AwardedBy = ba.AwardedBy ?? string.Empty,
                GeneratedAt = DateTime.UtcNow
            })
            .ToListAsync();

        return WriteCsv(rows);
    }

    public async Task<byte[]> GetExecutiveSummaryReportAsync(Guid eventId)
    {
        var evt = await dbContext.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (evt is null)
            return [];

        var totalKm = await dbContext.Activities
            .AsNoTracking()
            .Where(a => a.EventId == eventId && a.Status == ActivityStatus.Approved && a.CountsTowardTotal)
            .SumAsync(a => a.DistanceKm);

        var participantCount = await dbContext.Participants
            .AsNoTracking()
            .CountAsync(p => p.EventId == eventId && p.Status == ParticipantStatus.Active);

        var teamCount = await dbContext.Teams
            .AsNoTracking()
            .CountAsync(t => t.EventId == eventId);

        var milestonesReached = await dbContext.Milestones
            .AsNoTracking()
            .CountAsync(m => m.EventId == eventId && m.AchievedAt.HasValue);

        var totalMilestones = await dbContext.Milestones
            .AsNoTracking()
            .CountAsync(m => m.EventId == eventId);

        var progressPct = evt.RouteDistanceKm > 0
            ? Math.Round(totalKm / evt.RouteDistanceKm * 100, 2, MidpointRounding.AwayFromZero)
            : 0m;

        var topIndividuals = await dbContext.Participants
            .AsNoTracking()
            .Where(p => p.EventId == eventId && p.Status == ParticipantStatus.Active && p.LeaderboardOptIn)
            .Select(p => new
            {
                p.DisplayName,
                TotalKm = p.Activities
                    .Where(a => a.Status == ActivityStatus.Approved && a.CountsTowardTotal)
                    .Sum(a => a.DistanceKm)
            })
            .OrderByDescending(p => p.TotalKm)
            .Take(10)
            .ToListAsync();

        var topTeams = await dbContext.Teams
            .AsNoTracking()
            .Where(t => t.EventId == eventId)
            .Select(t => new
            {
                t.Name,
                TotalKm = t.Participants
                    .Where(p => p.Status == ParticipantStatus.Active)
                    .SelectMany(p => p.Activities)
                    .Where(a => a.Status == ActivityStatus.Approved && a.CountsTowardTotal)
                    .Sum(a => a.DistanceKm)
            })
            .OrderByDescending(t => t.TotalKm)
            .Take(10)
            .ToListAsync();

        var generatedAt = DateTime.UtcNow;

        var summaryRows = new List<ExecutiveSummaryReportRow>
        {
            new()
            {
                Section = "Headline",
                Label = "Event Name",
                Value = evt.Name,
                GeneratedAt = generatedAt
            },
            new()
            {
                Section = "Headline",
                Label = "Event Status",
                Value = evt.Status.ToString(),
                GeneratedAt = generatedAt
            },
            new()
            {
                Section = "Headline",
                Label = "Total Km Ridden",
                Value = totalKm.ToString("F2", CultureInfo.InvariantCulture),
                GeneratedAt = generatedAt
            },
            new()
            {
                Section = "Headline",
                Label = "Route Progress (%)",
                Value = progressPct.ToString("F2", CultureInfo.InvariantCulture),
                GeneratedAt = generatedAt
            },
            new()
            {
                Section = "Headline",
                Label = "Active Participants",
                Value = participantCount.ToString(),
                GeneratedAt = generatedAt
            },
            new()
            {
                Section = "Headline",
                Label = "Teams",
                Value = teamCount.ToString(),
                GeneratedAt = generatedAt
            },
            new()
            {
                Section = "Headline",
                Label = "Milestones Reached",
                Value = $"{milestonesReached} / {totalMilestones}",
                GeneratedAt = generatedAt
            }
        };

        for (var i = 0; i < topIndividuals.Count; i++)
        {
            summaryRows.Add(new ExecutiveSummaryReportRow
            {
                Section = "Top Individual Leaderboard",
                Label = $"#{i + 1} {topIndividuals[i].DisplayName}",
                Value = topIndividuals[i].TotalKm.ToString("F2", CultureInfo.InvariantCulture) + " km",
                GeneratedAt = generatedAt
            });
        }

        for (var i = 0; i < topTeams.Count; i++)
        {
            summaryRows.Add(new ExecutiveSummaryReportRow
            {
                Section = "Top Team Leaderboard",
                Label = $"#{i + 1} {topTeams[i].Name}",
                Value = topTeams[i].TotalKm.ToString("F2", CultureInfo.InvariantCulture) + " km",
                GeneratedAt = generatedAt
            });
        }

        return WriteCsv(summaryRows);
    }

    private static byte[] WriteCsv<T>(IEnumerable<T> records)
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms);
        using var csv = new CsvWriter(writer, CsvConfig);
        csv.WriteRecords(records);
        writer.Flush();
        return ms.ToArray();
    }

    private sealed class ParticipantReportRow
    {
        public string DisplayName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string WorkEmail { get; set; } = string.Empty;
        public string TeamName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
        public decimal TotalKm { get; set; }
        public int RideCount { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    private sealed class ActivityReportRow
    {
        public string ParticipantDisplayName { get; set; } = string.Empty;
        public string TeamName { get; set; } = string.Empty;
        public DateTime ActivityDate { get; set; }
        public decimal DistanceKm { get; set; }
        public string RideType { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool CountsTowardTotal { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string ApprovedBy { get; set; } = string.Empty;
        public DateTime? ApprovedAt { get; set; }
        public string RejectionReason { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
    }

    private sealed class TeamTotalsReportRow
    {
        public string TeamName { get; set; } = string.Empty;
        public int ActiveParticipantCount { get; set; }
        public int TotalParticipantCount { get; set; }
        public decimal TotalKm { get; set; }
        public decimal AverageKmPerActiveParticipant { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    private sealed class MilestoneReportRow
    {
        public string StopName { get; set; } = string.Empty;
        public int OrderIndex { get; set; }
        public decimal CumulativeDistanceKm { get; set; }
        public string Description { get; set; } = string.Empty;
        public string RewardText { get; set; } = string.Empty;
        public bool IsAchieved { get; set; }
        public DateTime? AchievedAt { get; set; }
        public decimal? TotalKmAtAchievement { get; set; }
        public string AnnouncementStatus { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
    }

    private sealed class BadgeAwardReportRow
    {
        public string BadgeName { get; set; } = string.Empty;
        public decimal ThresholdKm { get; set; }
        public string ParticipantDisplayName { get; set; } = string.Empty;
        public string TeamName { get; set; } = string.Empty;
        public DateTime AwardedAt { get; set; }
        public bool IsManual { get; set; }
        public string AwardedBy { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
    }

    private sealed class ExecutiveSummaryReportRow
    {
        public string Section { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
    }
}
