using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Server.Domain.Entities;
using PedalAcrossCanada.Server.Infrastructure.Data;
using PedalAcrossCanada.Shared.DTOs.Teams;

namespace PedalAcrossCanada.Server.Application.Services;

public class TeamService(AppDbContext dbContext, IAuditService auditService) : ITeamService
{
    public async Task<IReadOnlyList<TeamDto>> GetAllByEventAsync(Guid eventId)
    {
        await EnsureEventExistsAsync(eventId);

        var teams = await dbContext.Teams
            .AsNoTracking()
            .Where(t => t.EventId == eventId)
            .Include(t => t.Captain)
            .OrderBy(t => t.Name)
            .ToListAsync();

        var memberCounts = await dbContext.Participants
            .Where(p => p.EventId == eventId && p.TeamId != null)
            .GroupBy(p => p.TeamId!.Value)
            .Select(g => new { TeamId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TeamId, x => x.Count);

        return teams.Select(t => MapToDto(t, memberCounts.GetValueOrDefault(t.Id, 0))).ToList();
    }

    public async Task<TeamDto> GetByIdAsync(Guid eventId, Guid teamId)
    {
        var team = await dbContext.Teams
            .AsNoTracking()
            .Include(t => t.Captain)
            .FirstOrDefaultAsync(t => t.Id == teamId && t.EventId == eventId)
            ?? throw new KeyNotFoundException($"Team with id '{teamId}' not found in event '{eventId}'.");

        var memberCount = await dbContext.Participants.CountAsync(p => p.TeamId == teamId);
        return MapToDto(team, memberCount);
    }

    public async Task<TeamDto> CreateAsync(Guid eventId, CreateTeamRequest request, string actor)
    {
        await EnsureEventExistsAsync(eventId);

        var nameExists = await dbContext.Teams
            .AnyAsync(t => t.EventId == eventId && t.Name == request.Name);
        if (nameExists)
            throw new ArgumentException($"A team named '{request.Name}' already exists for this event.");

        var team = new Team
        {
            EventId = eventId,
            Name = request.Name,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Teams.Add(team);
        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "TeamCreated", "Team", team.Id.ToString(),
            eventId, null, JsonSerializer.Serialize(team));

        return MapToDto(team, 0);
    }

    public async Task<TeamDto> UpdateAsync(Guid eventId, Guid teamId, UpdateTeamRequest request, string actor)
    {
        var team = await GetTrackedTeamAsync(eventId, teamId);

        var nameExists = await dbContext.Teams
            .AnyAsync(t => t.EventId == eventId && t.Name == request.Name && t.Id != teamId);
        if (nameExists)
            throw new ArgumentException($"A team named '{request.Name}' already exists for this event.");

        var before = JsonSerializer.Serialize(team);

        team.Name = request.Name;
        team.Description = request.Description;
        team.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "TeamUpdated", "Team", team.Id.ToString(),
            eventId, before, JsonSerializer.Serialize(team));

        var memberCount = await dbContext.Participants.CountAsync(p => p.TeamId == teamId);
        return MapToDto(team, memberCount);
    }

    public async Task DeleteAsync(Guid eventId, Guid teamId, string actor)
    {
        var team = await GetTrackedTeamAsync(eventId, teamId);

        var hasMembers = await dbContext.Participants.AnyAsync(p => p.TeamId == teamId);
        if (hasMembers)
            throw new InvalidOperationException("Cannot delete a team that still has members. Reassign or remove members first.");

        var before = JsonSerializer.Serialize(team);

        dbContext.Teams.Remove(team);
        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "TeamDeleted", "Team", teamId.ToString(),
            eventId, before, null);
    }

    public async Task<TeamDto> SetCaptainAsync(Guid eventId, Guid teamId, Guid participantId, string actor)
    {
        var team = await GetTrackedTeamAsync(eventId, teamId);

        var participant = await dbContext.Participants
            .FirstOrDefaultAsync(p => p.Id == participantId && p.EventId == eventId)
            ?? throw new KeyNotFoundException($"Participant with id '{participantId}' not found in event '{eventId}'.");

        var before = JsonSerializer.Serialize(team);

        team.CaptainParticipantId = participantId;
        team.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "TeamCaptainSet", "Team", team.Id.ToString(),
            eventId, before, JsonSerializer.Serialize(team));

        var memberCount = await dbContext.Participants.CountAsync(p => p.TeamId == teamId);

        // Reload captain nav property for DTO mapping
        await dbContext.Entry(team).Reference(t => t.Captain).LoadAsync();
        return MapToDto(team, memberCount);
    }

    private async Task<Team> GetTrackedTeamAsync(Guid eventId, Guid teamId)
    {
        return await dbContext.Teams
            .FirstOrDefaultAsync(t => t.Id == teamId && t.EventId == eventId)
            ?? throw new KeyNotFoundException($"Team with id '{teamId}' not found in event '{eventId}'.");
    }

    private async Task EnsureEventExistsAsync(Guid eventId)
    {
        var exists = await dbContext.Events.AnyAsync(e => e.Id == eventId);
        if (!exists) throw new KeyNotFoundException($"Event with id '{eventId}' not found.");
    }

    private static TeamDto MapToDto(Team team, int memberCount) => new()
    {
        Id = team.Id,
        EventId = team.EventId,
        Name = team.Name,
        Description = team.Description,
        CaptainParticipantId = team.CaptainParticipantId,
        CaptainDisplayName = team.Captain?.DisplayName,
        MemberCount = memberCount,
        CreatedAt = team.CreatedAt,
        UpdatedAt = team.UpdatedAt
    };
}
