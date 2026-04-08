using Microsoft.EntityFrameworkCore;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Server.Infrastructure.Data;
using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Leaderboards;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Application.Services;

public class LeaderboardService(AppDbContext dbContext) : ILeaderboardService
{
    public async Task<PagedResult<IndividualLeaderboardEntry>> GetIndividualLeaderboardAsync(
        Guid eventId, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var rankedEntries = await GetRankedIndividualEntriesAsync(eventId);

        var totalCount = rankedEntries.Count;
        var paged = rankedEntries
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return PagedResult<IndividualLeaderboardEntry>.Create(paged, page, pageSize, totalCount);
    }

    public async Task<IReadOnlyList<TeamLeaderboardEntry>> GetTeamLeaderboardAsync(Guid eventId)
    {
        var teamStats = await dbContext.Teams
            .AsNoTracking()
            .Where(t => t.EventId == eventId)
            .Select(t => new
            {
                t.Id,
                t.Name,
                ActiveParticipants = t.Participants.Count(p => p.Status == ParticipantStatus.Active),
                TotalKm = t.Participants
                    .Where(p => p.Status == ParticipantStatus.Active)
                    .SelectMany(p => p.Activities)
                    .Where(a => a.Status == ActivityStatus.Approved && a.CountsTowardTotal)
                    .Sum(a => a.DistanceKm)
            })
            .OrderByDescending(t => t.TotalKm)
            .ThenBy(t => t.Name)
            .ToListAsync();

        var entries = new List<TeamLeaderboardEntry>();
        var rank = 1;

        for (var i = 0; i < teamStats.Count; i++)
        {
            var t = teamStats[i];

            if (i > 0 && t.TotalKm != teamStats[i - 1].TotalKm)
            {
                rank = i + 1;
            }

            entries.Add(new TeamLeaderboardEntry
            {
                Rank = rank,
                TeamId = t.Id,
                TeamName = t.Name,
                TotalKm = t.TotalKm,
                ActiveParticipants = t.ActiveParticipants,
                AverageKmPerParticipant = t.ActiveParticipants > 0
                    ? Math.Round(t.TotalKm / t.ActiveParticipants, 2, MidpointRounding.AwayFromZero)
                    : 0m
            });
        }

        return entries;
    }

    public async Task<int?> GetParticipantRankAsync(Guid eventId, Guid participantId)
    {
        var rankedEntries = await GetRankedIndividualEntriesAsync(eventId);
        var entry = rankedEntries.FirstOrDefault(e => e.ParticipantId == participantId);
        return entry?.Rank;
    }

    public async Task<int?> GetTeamRankAsync(Guid eventId, Guid teamId)
    {
        var teamEntries = await GetTeamLeaderboardAsync(eventId);
        var entry = teamEntries.FirstOrDefault(e => e.TeamId == teamId);
        return entry?.Rank;
    }

    private async Task<List<IndividualLeaderboardEntry>> GetRankedIndividualEntriesAsync(Guid eventId)
    {
        // Only active participants who opted in to the leaderboard
        var participantStats = await dbContext.Participants
            .AsNoTracking()
            .Where(p => p.EventId == eventId
                        && p.Status == ParticipantStatus.Active
                        && p.LeaderboardOptIn)
            .Select(p => new
            {
                p.Id,
                p.DisplayName,
                TeamName = p.Team != null ? p.Team.Name : null,
                p.JoinedAt,
                TotalKm = p.Activities
                    .Where(a => a.Status == ActivityStatus.Approved && a.CountsTowardTotal)
                    .Sum(a => a.DistanceKm),
                RideCount = p.Activities
                    .Count(a => a.Status == ActivityStatus.Approved && a.CountsTowardTotal)
            })
            .OrderByDescending(p => p.TotalKm)
            .ThenByDescending(p => p.RideCount)
            .ThenBy(p => p.JoinedAt)
            .ToListAsync();

        var entries = new List<IndividualLeaderboardEntry>();
        var rank = 1;

        for (var i = 0; i < participantStats.Count; i++)
        {
            var p = participantStats[i];

            // Tie-breaking: same rank if totalKm, rideCount, and joinedAt all match
            if (i > 0)
            {
                var prev = participantStats[i - 1];
                if (p.TotalKm != prev.TotalKm || p.RideCount != prev.RideCount || p.JoinedAt != prev.JoinedAt)
                {
                    rank = i + 1;
                }
            }

            entries.Add(new IndividualLeaderboardEntry
            {
                Rank = rank,
                ParticipantId = p.Id,
                DisplayName = p.DisplayName,
                TeamName = p.TeamName,
                TotalKm = p.TotalKm,
                RideCount = p.RideCount,
                JoinedAt = p.JoinedAt
            });
        }

        return entries;
    }
}
