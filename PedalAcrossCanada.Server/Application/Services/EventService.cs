using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Server.Domain.Entities;
using PedalAcrossCanada.Server.Infrastructure.Data;
using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Events;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Application.Services;

public class EventService(AppDbContext dbContext, IAuditService auditService) : IEventService
{
    public async Task<PagedResult<EventDto>> GetAllAsync(int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.Events.AsNoTracking();
        var totalCount = await query.CountAsync();

        var events = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => MapToDto(e,
                e.Milestones.Count,
                e.Participants.Count))
            .ToListAsync();

        return PagedResult<EventDto>.Create(events, page, pageSize, totalCount);
    }

    public async Task<EventDto> GetByIdAsync(Guid eventId)
    {
        var ev = await dbContext.Events
            .AsNoTracking()
            .Include(e => e.Milestones)
            .Include(e => e.Participants)
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event with id '{eventId}' not found.");

        return MapToDto(ev, ev.Milestones.Count, ev.Participants.Count);
    }

    public async Task<EventDto> CreateAsync(CreateEventRequest request, string actor)
    {
        ValidateDateRange(request.StartDate, request.EndDate);

        var ev = new Event
        {
            Name = request.Name,
            Description = request.Description,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            RouteDistanceKm = request.RouteDistanceKm,
            Status = EventStatus.Draft,
            ManualEntryMode = request.ManualEntryMode,
            StravaEnabled = request.StravaEnabled,
            BannerMessage = request.BannerMessage,
            MaxSingleRideKm = request.MaxSingleRideKm,
            LeaderboardPublic = request.LeaderboardPublic,
            ShowTeamAverage = request.ShowTeamAverage,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Events.Add(ev);
        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "EventCreated", "Event", ev.Id.ToString(),
            ev.Id, null, JsonSerializer.Serialize(ev));

        return MapToDto(ev, 0, 0);
    }

    public async Task<EventDto> UpdateAsync(Guid eventId, UpdateEventRequest request, string actor)
    {
        var ev = await GetTrackedEventAsync(eventId);

        ValidateDateRange(request.StartDate, request.EndDate);

        var before = JsonSerializer.Serialize(ev);

        ev.Name = request.Name;
        ev.Description = request.Description;
        ev.StartDate = request.StartDate;
        ev.EndDate = request.EndDate;
        ev.RouteDistanceKm = request.RouteDistanceKm;
        ev.ManualEntryMode = request.ManualEntryMode;
        ev.StravaEnabled = request.StravaEnabled;
        ev.BannerMessage = request.BannerMessage;
        ev.MaxSingleRideKm = request.MaxSingleRideKm;
        ev.LeaderboardPublic = request.LeaderboardPublic;
        ev.ShowTeamAverage = request.ShowTeamAverage;
        ev.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "EventUpdated", "Event", ev.Id.ToString(),
            ev.Id, before, JsonSerializer.Serialize(ev));

        var milestoneCount = await dbContext.Milestones.CountAsync(m => m.EventId == eventId);
        var participantCount = await dbContext.Participants.CountAsync(p => p.EventId == eventId);
        return MapToDto(ev, milestoneCount, participantCount);
    }

    public async Task<EventDto> ActivateAsync(Guid eventId, string actor)
    {
        var ev = await GetTrackedEventAsync(eventId);

        if (ev.Status != EventStatus.Draft)
            throw new InvalidOperationException("Only Draft events can be activated.");

        var hasMilestones = await dbContext.Milestones.AnyAsync(m => m.EventId == eventId);
        if (!hasMilestones)
            throw new InvalidOperationException("Event cannot be activated without at least one milestone.");

        var hasOtherActive = await dbContext.Events
            .AnyAsync(e => e.Id != eventId && e.Status == EventStatus.Active);
        if (hasOtherActive)
            throw new InvalidOperationException("Only one Active event is allowed at a time.");

        var before = JsonSerializer.Serialize(ev);

        ev.Status = EventStatus.Active;
        ev.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "EventActivated", "Event", ev.Id.ToString(),
            ev.Id, before, JsonSerializer.Serialize(ev));

        var milestoneCount = await dbContext.Milestones.CountAsync(m => m.EventId == eventId);
        var participantCount = await dbContext.Participants.CountAsync(p => p.EventId == eventId);
        return MapToDto(ev, milestoneCount, participantCount);
    }

    public async Task<EventDto> CloseAsync(Guid eventId, string actor)
    {
        var ev = await GetTrackedEventAsync(eventId);

        if (ev.Status != EventStatus.Active)
            throw new InvalidOperationException("Only Active events can be closed.");

        var before = JsonSerializer.Serialize(ev);

        ev.Status = EventStatus.Closed;
        ev.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "EventClosed", "Event", ev.Id.ToString(),
            ev.Id, before, JsonSerializer.Serialize(ev));

        var milestoneCount = await dbContext.Milestones.CountAsync(m => m.EventId == eventId);
        var participantCount = await dbContext.Participants.CountAsync(p => p.EventId == eventId);
        return MapToDto(ev, milestoneCount, participantCount);
    }

    public async Task<EventDto> ArchiveAsync(Guid eventId, string actor)
    {
        var ev = await GetTrackedEventAsync(eventId);

        if (ev.Status != EventStatus.Closed)
            throw new InvalidOperationException("Only Closed events can be archived.");

        var before = JsonSerializer.Serialize(ev);

        ev.Status = EventStatus.Archived;
        ev.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "EventArchived", "Event", ev.Id.ToString(),
            ev.Id, before, JsonSerializer.Serialize(ev));

        var milestoneCount = await dbContext.Milestones.CountAsync(m => m.EventId == eventId);
        var participantCount = await dbContext.Participants.CountAsync(p => p.EventId == eventId);
        return MapToDto(ev, milestoneCount, participantCount);
    }

    public async Task<EventDto> RevertToDraftAsync(Guid eventId, string actor)
    {
        var ev = await GetTrackedEventAsync(eventId);

        if (ev.Status != EventStatus.Active)
            throw new InvalidOperationException("Only Active events can be reverted to Draft.");

        var hasActivities = await dbContext.Activities.AnyAsync(a => a.EventId == eventId);
        if (hasActivities)
            throw new InvalidOperationException("Cannot revert to Draft when activities exist.");

        var before = JsonSerializer.Serialize(ev);

        ev.Status = EventStatus.Draft;
        ev.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "EventRevertedToDraft", "Event", ev.Id.ToString(),
            ev.Id, before, JsonSerializer.Serialize(ev));

        var milestoneCount = await dbContext.Milestones.CountAsync(m => m.EventId == eventId);
        var participantCount = await dbContext.Participants.CountAsync(p => p.EventId == eventId);
        return MapToDto(ev, milestoneCount, participantCount);
    }

    public async Task<EventDto?> GetActiveEventAsync()
    {
        var ev = await dbContext.Events
            .AsNoTracking()
            .Include(e => e.Milestones)
            .Include(e => e.Participants)
            .FirstOrDefaultAsync(e => e.Status == EventStatus.Active);

        if (ev is null) return null;
        return MapToDto(ev, ev.Milestones.Count, ev.Participants.Count);
    }

    private async Task<Event> GetTrackedEventAsync(Guid eventId)
    {
        return await dbContext.Events
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event with id '{eventId}' not found.");
    }

    private static void ValidateDateRange(DateTime start, DateTime end)
    {
        if (end < start)
            throw new ArgumentException("End date must be on or after the start date.");
    }

    private static EventDto MapToDto(Event ev, int milestoneCount, int participantCount) => new()
    {
        Id = ev.Id,
        Name = ev.Name,
        Description = ev.Description,
        StartDate = ev.StartDate,
        EndDate = ev.EndDate,
        RouteDistanceKm = ev.RouteDistanceKm,
        Status = ev.Status,
        ManualEntryMode = ev.ManualEntryMode,
        StravaEnabled = ev.StravaEnabled,
        BannerMessage = ev.BannerMessage,
        MaxSingleRideKm = ev.MaxSingleRideKm,
        LeaderboardPublic = ev.LeaderboardPublic,
        ShowTeamAverage = ev.ShowTeamAverage,
        CreatedAt = ev.CreatedAt,
        UpdatedAt = ev.UpdatedAt,
        MilestoneCount = milestoneCount,
        ParticipantCount = participantCount
    };
}
