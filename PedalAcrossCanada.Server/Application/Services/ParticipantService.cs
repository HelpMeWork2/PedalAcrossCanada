using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Server.Domain.Entities;
using PedalAcrossCanada.Server.Infrastructure.Data;
using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Participants;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Application.Services;

public class ParticipantService(AppDbContext dbContext, IAuditService auditService) : IParticipantService
{
    public async Task<PagedResult<ParticipantDto>> GetAllByEventAsync(Guid eventId, int page, int pageSize)
    {
        await EnsureEventExistsAsync(eventId);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.Participants
            .AsNoTracking()
            .Where(p => p.EventId == eventId)
            .Include(p => p.Team)
            .Include(p => p.ExternalConnections);

        var totalCount = await query.CountAsync();

        var participants = await query
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = participants.Select(MapToDto).ToList();
        return PagedResult<ParticipantDto>.Create(dtos, page, pageSize, totalCount);
    }

    public async Task<ParticipantDto> GetByIdAsync(Guid eventId, Guid participantId)
    {
        var participant = await dbContext.Participants
            .AsNoTracking()
            .Include(p => p.Team)
            .Include(p => p.ExternalConnections)
            .FirstOrDefaultAsync(p => p.Id == participantId && p.EventId == eventId)
            ?? throw new KeyNotFoundException($"Participant with id '{participantId}' not found in event '{eventId}'.");

        return MapToDto(participant);
    }

    public async Task<ParticipantDto> GetByUserIdAsync(Guid eventId, string userId)
    {
        var participant = await dbContext.Participants
            .AsNoTracking()
            .Include(p => p.Team)
            .Include(p => p.ExternalConnections)
            .FirstOrDefaultAsync(p => p.UserId == userId && p.EventId == eventId)
            ?? throw new KeyNotFoundException("You are not registered for this event.");

        return MapToDto(participant);
    }

    public async Task<ParticipantDto> CreateAsync(
        Guid eventId, CreateParticipantRequest request, string userId, string actor)
    {
        var ev = await dbContext.Events.FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event with id '{eventId}' not found.");

        if (ev.Status is EventStatus.Closed or EventStatus.Archived)
            throw new InvalidOperationException("Cannot register for a Closed or Archived event.");

        var duplicateEmail = await dbContext.Participants
            .AnyAsync(p => p.EventId == eventId && p.WorkEmail == request.WorkEmail && p.Status == ParticipantStatus.Active);
        if (duplicateEmail)
            throw new ArgumentException("A participant with this email is already registered for this event.");

        var duplicateUser = await dbContext.Participants
            .AnyAsync(p => p.EventId == eventId && p.UserId == userId && p.Status == ParticipantStatus.Active);
        if (duplicateUser)
            throw new ArgumentException("You are already registered for this event.");

        if (request.TeamId.HasValue)
        {
            var teamExists = await dbContext.Teams
                .AnyAsync(t => t.Id == request.TeamId.Value && t.EventId == eventId);
            if (!teamExists)
                throw new KeyNotFoundException($"Team with id '{request.TeamId}' not found in event '{eventId}'.");
        }

        var participant = new Participant
        {
            EventId = eventId,
            UserId = userId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            WorkEmail = request.WorkEmail,
            DisplayName = request.DisplayName,
            TeamId = request.TeamId,
            Status = ParticipantStatus.Active,
            JoinedAt = DateTime.UtcNow,
            LeaderboardOptIn = request.LeaderboardOptIn,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Participants.Add(participant);

        if (request.TeamId.HasValue)
        {
            dbContext.TeamHistories.Add(new TeamHistory
            {
                ParticipantId = participant.Id,
                TeamId = request.TeamId.Value,
                EffectiveFrom = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "ParticipantCreated", "Participant", participant.Id.ToString(),
            eventId, null, JsonSerializer.Serialize(new
            {
                participant.Id,
                participant.UserId,
                participant.EventId,
                participant.FirstName,
                participant.LastName,
                participant.WorkEmail,
                participant.DisplayName,
                participant.TeamId,
                participant.Status,
                participant.JoinedAt
            }));

        // Reload nav properties for DTO mapping
        if (participant.TeamId.HasValue)
            await dbContext.Entry(participant).Reference(p => p.Team).LoadAsync();
        await dbContext.Entry(participant).Collection(p => p.ExternalConnections).LoadAsync();

        return MapToDto(participant);
    }

    public async Task<ParticipantDto> UpdateAsync(
        Guid eventId, Guid participantId, UpdateParticipantRequest request, string actor)
    {
        var participant = await GetTrackedParticipantAsync(eventId, participantId);
        var before = JsonSerializer.Serialize(participant);

        participant.FirstName = request.FirstName;
        participant.LastName = request.LastName;
        participant.DisplayName = request.DisplayName;
        participant.LeaderboardOptIn = request.LeaderboardOptIn;
        participant.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "ParticipantUpdated", "Participant", participant.Id.ToString(),
            eventId, before, JsonSerializer.Serialize(participant));

        await dbContext.Entry(participant).Reference(p => p.Team).LoadAsync();
        await dbContext.Entry(participant).Collection(p => p.ExternalConnections).LoadAsync();
        return MapToDto(participant);
    }

    public async Task<ParticipantDto> DeactivateAsync(Guid eventId, Guid participantId, string actor)
    {
        var participant = await GetTrackedParticipantAsync(eventId, participantId);

        if (participant.Status == ParticipantStatus.Inactive)
            throw new InvalidOperationException("Participant is already inactive.");

        var before = JsonSerializer.Serialize(participant);

        participant.Status = ParticipantStatus.Inactive;
        participant.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "ParticipantDeactivated", "Participant", participant.Id.ToString(),
            eventId, before, JsonSerializer.Serialize(participant));

        await dbContext.Entry(participant).Reference(p => p.Team).LoadAsync();
        await dbContext.Entry(participant).Collection(p => p.ExternalConnections).LoadAsync();
        return MapToDto(participant);
    }

    public async Task<ParticipantDto> ReactivateAsync(Guid eventId, Guid participantId, string actor)
    {
        var participant = await GetTrackedParticipantAsync(eventId, participantId);

        if (participant.Status == ParticipantStatus.Active)
            throw new InvalidOperationException("Participant is already active.");

        var before = JsonSerializer.Serialize(participant);

        participant.Status = ParticipantStatus.Active;
        participant.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "ParticipantReactivated", "Participant", participant.Id.ToString(),
            eventId, before, JsonSerializer.Serialize(participant));

        await dbContext.Entry(participant).Reference(p => p.Team).LoadAsync();
        await dbContext.Entry(participant).Collection(p => p.ExternalConnections).LoadAsync();
        return MapToDto(participant);
    }

    public async Task<ParticipantDto> ChangeTeamAsync(
        Guid eventId, Guid participantId, Guid teamId, string actor)
    {
        var participant = await GetTrackedParticipantAsync(eventId, participantId);

        var teamExists = await dbContext.Teams
            .AnyAsync(t => t.Id == teamId && t.EventId == eventId);
        if (!teamExists)
            throw new KeyNotFoundException($"Team with id '{teamId}' not found in event '{eventId}'.");

        var before = JsonSerializer.Serialize(new
        {
            participant.Id,
            participant.UserId,
            participant.EventId,
            participant.TeamId,
            participant.Status
        });

        participant.TeamId = teamId;
        participant.UpdatedAt = DateTime.UtcNow;

        dbContext.TeamHistories.Add(new TeamHistory
        {
            ParticipantId = participantId,
            TeamId = teamId,
            EffectiveFrom = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "ParticipantTeamChanged", "Participant", participant.Id.ToString(),
            eventId, before, JsonSerializer.Serialize(new
            {
                participant.Id,
                participant.UserId,
                participant.EventId,
                participant.TeamId,
                participant.Status
            }));

        await dbContext.Entry(participant).Reference(p => p.Team).LoadAsync();
        await dbContext.Entry(participant).Collection(p => p.ExternalConnections).LoadAsync();
        return MapToDto(participant);
    }

    public async Task DeleteAsync(Guid eventId, Guid participantId, string actor)
    {
        var participant = await dbContext.Participants
            .Include(p => p.ExternalConnections)
            .FirstOrDefaultAsync(p => p.Id == participantId && p.EventId == eventId)
            ?? throw new KeyNotFoundException($"Participant with id '{participantId}' not found in event '{eventId}'.");

        var before = JsonSerializer.Serialize(participant);

        // Remove related data
        var activities = await dbContext.Activities
            .Where(a => a.ParticipantId == participantId)
            .ToListAsync();
        dbContext.Activities.RemoveRange(activities);

        var teamHistories = await dbContext.TeamHistories
            .Where(th => th.ParticipantId == participantId)
            .ToListAsync();
        dbContext.TeamHistories.RemoveRange(teamHistories);

        var badgeAwards = await dbContext.BadgeAwards
            .Where(ba => ba.ParticipantId == participantId)
            .ToListAsync();
        dbContext.BadgeAwards.RemoveRange(badgeAwards);

        if (participant.ExternalConnections?.Count > 0)
        {
            dbContext.ExternalConnections.RemoveRange(participant.ExternalConnections);
        }

        dbContext.Participants.Remove(participant);

        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "ParticipantDeleted", "Participant", participant.Id.ToString(),
            eventId, before, null);
    }

    private async Task<Participant> GetTrackedParticipantAsync(Guid eventId, Guid participantId)
    {
        return await dbContext.Participants
            .FirstOrDefaultAsync(p => p.Id == participantId && p.EventId == eventId)
            ?? throw new KeyNotFoundException($"Participant with id '{participantId}' not found in event '{eventId}'.");
    }

    private async Task EnsureEventExistsAsync(Guid eventId)
    {
        var exists = await dbContext.Events.AnyAsync(e => e.Id == eventId);
        if (!exists) throw new KeyNotFoundException($"Event with id '{eventId}' not found.");
    }

    private static ParticipantDto MapToDto(Participant p)
    {
        var stravaConnection = p.ExternalConnections?
            .FirstOrDefault(ec => ec.Provider == "Strava");

        return new ParticipantDto
        {
            Id = p.Id,
            EventId = p.EventId,
            UserId = p.UserId,
            FirstName = p.FirstName,
            LastName = p.LastName,
            WorkEmail = p.WorkEmail,
            DisplayName = p.DisplayName,
            TeamId = p.TeamId,
            TeamName = p.Team?.Name,
            Status = p.Status,
            JoinedAt = p.JoinedAt,
            LeaderboardOptIn = p.LeaderboardOptIn,
            StravaConsentGiven = p.StravaConsentGiven,
            StravaConnected = stravaConnection?.ConnectionStatus == ConnectionStatus.Connected,
            StravaConnectionStatus = stravaConnection?.ConnectionStatus,
            StravaLastSyncAt = stravaConnection?.LastSyncAt,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };
    }
}
