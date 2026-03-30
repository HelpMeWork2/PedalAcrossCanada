using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Server.Domain.Entities;
using PedalAcrossCanada.Server.Infrastructure.Data;
using PedalAcrossCanada.Shared.DTOs.Milestones;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Application.Services;

public class MilestoneService(AppDbContext dbContext, IAuditService auditService) : IMilestoneService
{
    public async Task<IReadOnlyList<MilestoneDto>> GetAllByEventAsync(Guid eventId)
    {
        await EnsureEventExistsAsync(eventId);

        var milestones = await dbContext.Milestones
            .AsNoTracking()
            .Where(m => m.EventId == eventId)
            .OrderBy(m => m.OrderIndex)
            .ToListAsync();

        return milestones.Select(MapToDto).ToList();
    }

    public async Task<MilestoneDto> GetByIdAsync(Guid eventId, Guid milestoneId)
    {
        var milestone = await GetTrackedMilestoneAsync(eventId, milestoneId);
        return MapToDto(milestone);
    }

    public async Task<MilestoneDto> CreateAsync(Guid eventId, CreateMilestoneRequest request, string actor)
    {
        var ev = await dbContext.Events.FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event with id '{eventId}' not found.");

        if (ev.Status != EventStatus.Draft)
            throw new InvalidOperationException("Milestones can only be created when the event is in Draft status.");

        await ValidateAscendingDistanceAsync(eventId, request.CumulativeDistanceKm, excludeId: null);
        await ValidateUniqueOrderIndexAsync(eventId, request.OrderIndex, excludeId: null);

        var milestone = new Milestone
        {
            EventId = eventId,
            StopName = request.StopName,
            OrderIndex = request.OrderIndex,
            CumulativeDistanceKm = request.CumulativeDistanceKm,
            Description = request.Description,
            RewardText = request.RewardText,
            AnnouncementStatus = AnnouncementStatus.Pending
        };

        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "MilestoneCreated", "Milestone", milestone.Id.ToString(),
            eventId, null, JsonSerializer.Serialize(milestone));

        return MapToDto(milestone);
    }

    public async Task<MilestoneDto> UpdateAsync(
        Guid eventId, Guid milestoneId, UpdateMilestoneRequest request, string actor)
    {
        var ev = await dbContext.Events.FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event with id '{eventId}' not found.");

        var milestone = await GetTrackedMilestoneAsync(eventId, milestoneId);
        var before = JsonSerializer.Serialize(milestone);

        if (ev.Status != EventStatus.Draft)
        {
            // After activation, admin can only edit description/reward text
            if (milestone.CumulativeDistanceKm != request.CumulativeDistanceKm)
                throw new InvalidOperationException("Cumulative distance cannot be changed after event activation. Revert to Draft first.");

            if (milestone.OrderIndex != request.OrderIndex)
                throw new InvalidOperationException("Order index cannot be changed after event activation. Revert to Draft first.");

            if (milestone.StopName != request.StopName)
                throw new InvalidOperationException("Stop name cannot be changed after event activation. Revert to Draft first.");

            milestone.Description = request.Description;
            milestone.RewardText = request.RewardText;
        }
        else
        {
            await ValidateAscendingDistanceAsync(eventId, request.CumulativeDistanceKm, excludeId: milestoneId);
            await ValidateUniqueOrderIndexAsync(eventId, request.OrderIndex, excludeId: milestoneId);

            milestone.StopName = request.StopName;
            milestone.OrderIndex = request.OrderIndex;
            milestone.CumulativeDistanceKm = request.CumulativeDistanceKm;
            milestone.Description = request.Description;
            milestone.RewardText = request.RewardText;
        }

        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "MilestoneUpdated", "Milestone", milestone.Id.ToString(),
            eventId, before, JsonSerializer.Serialize(milestone));

        return MapToDto(milestone);
    }

    public async Task DeleteAsync(Guid eventId, Guid milestoneId, string actor)
    {
        var ev = await dbContext.Events.FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event with id '{eventId}' not found.");

        if (ev.Status != EventStatus.Draft)
            throw new InvalidOperationException("Milestones can only be deleted when the event is in Draft status.");

        var milestone = await GetTrackedMilestoneAsync(eventId, milestoneId);
        var before = JsonSerializer.Serialize(milestone);

        dbContext.Milestones.Remove(milestone);
        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "MilestoneDeleted", "Milestone", milestoneId.ToString(),
            eventId, before, null);
    }

    public async Task<MilestoneDto> AnnounceAsync(Guid eventId, Guid milestoneId, string actor)
    {
        var milestone = await GetTrackedMilestoneAsync(eventId, milestoneId);

        if (milestone.AchievedAt is null)
            throw new InvalidOperationException("Milestone has not been achieved yet.");

        if (milestone.AnnouncementStatus == AnnouncementStatus.Announced)
            throw new InvalidOperationException("Milestone has already been announced.");

        var before = JsonSerializer.Serialize(milestone);

        milestone.AnnouncementStatus = AnnouncementStatus.Announced;
        milestone.AnnouncedBy = actor;
        milestone.AnnouncedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "MilestoneAnnounced", "Milestone", milestoneId.ToString(),
            eventId, before, JsonSerializer.Serialize(milestone));

        return MapToDto(milestone);
    }

    private async Task<Milestone> GetTrackedMilestoneAsync(Guid eventId, Guid milestoneId)
    {
        return await dbContext.Milestones
            .FirstOrDefaultAsync(m => m.Id == milestoneId && m.EventId == eventId)
            ?? throw new KeyNotFoundException($"Milestone with id '{milestoneId}' not found in event '{eventId}'.");
    }

    private async Task EnsureEventExistsAsync(Guid eventId)
    {
        var exists = await dbContext.Events.AnyAsync(e => e.Id == eventId);
        if (!exists) throw new KeyNotFoundException($"Event with id '{eventId}' not found.");
    }

    private async Task ValidateAscendingDistanceAsync(Guid eventId, decimal cumulativeKm, Guid? excludeId)
    {
        var existingDistances = await dbContext.Milestones
            .AsNoTracking()
            .Where(m => m.EventId == eventId && (excludeId == null || m.Id != excludeId))
            .Select(m => m.CumulativeDistanceKm)
            .ToListAsync();

        if (existingDistances.Any(d => d == cumulativeKm))
            throw new ArgumentException($"A milestone with cumulative distance {cumulativeKm} km already exists for this event.");
    }

    private async Task ValidateUniqueOrderIndexAsync(Guid eventId, int orderIndex, Guid? excludeId)
    {
        var exists = await dbContext.Milestones
            .AsNoTracking()
            .AnyAsync(m => m.EventId == eventId
                && m.OrderIndex == orderIndex
                && (excludeId == null || m.Id != excludeId));

        if (exists)
            throw new ArgumentException($"A milestone with order index {orderIndex} already exists for this event.");
    }

    private static MilestoneDto MapToDto(Milestone m) => new()
    {
        Id = m.Id,
        EventId = m.EventId,
        StopName = m.StopName,
        OrderIndex = m.OrderIndex,
        CumulativeDistanceKm = m.CumulativeDistanceKm,
        Description = m.Description,
        RewardText = m.RewardText,
        AchievedAt = m.AchievedAt,
        TotalKmAtAchievement = m.TotalKmAtAchievement,
        AnnouncementStatus = m.AnnouncementStatus,
        AnnouncedBy = m.AnnouncedBy,
        AnnouncedAt = m.AnnouncedAt
    };
}
