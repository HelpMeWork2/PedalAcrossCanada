using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Server.Domain.Entities;
using PedalAcrossCanada.Server.Infrastructure.Data;
using PedalAcrossCanada.Shared.DTOs.Badges;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Application.Services;

public class BadgeService(AppDbContext dbContext, IAuditService auditService) : IBadgeService
{
    public async Task<IReadOnlyList<BadgeDto>> GetAllAsync(Guid eventId)
    {
        var badges = await dbContext.Badges
            .AsNoTracking()
            .OrderBy(b => b.SortOrder)
            .ToListAsync();

        var awardCounts = await dbContext.BadgeAwards
            .AsNoTracking()
            .Where(ba => ba.EventId == eventId)
            .GroupBy(ba => ba.BadgeId)
            .Select(g => new { BadgeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BadgeId, x => x.Count);

        return badges.Select(b => new BadgeDto
        {
            Id = b.Id,
            Name = b.Name,
            Description = b.Description,
            ThresholdKm = b.ThresholdKm,
            IsDefault = b.IsDefault,
            IsActive = b.IsActive,
            SortOrder = b.SortOrder,
            AwardCount = awardCounts.GetValueOrDefault(b.Id, 0)
        }).ToList();
    }

    public async Task<IReadOnlyList<BadgeAwardDto>> GetAwardsForParticipantAsync(
        Guid eventId, Guid participantId)
    {
        return await dbContext.BadgeAwards
            .AsNoTracking()
            .Include(ba => ba.Badge)
            .Where(ba => ba.ParticipantId == participantId && ba.EventId == eventId)
            .OrderBy(ba => ba.AwardedAt)
            .Select(ba => new BadgeAwardDto
            {
                Id = ba.Id,
                BadgeId = ba.BadgeId,
                BadgeName = ba.Badge.Name,
                BadgeDescription = ba.Badge.Description,
                ThresholdKm = ba.Badge.ThresholdKm,
                AwardedAt = ba.AwardedAt,
                IsManual = ba.IsManual,
                AwardedBy = ba.AwardedBy
            })
            .ToListAsync();
    }

    public async Task CheckAndAwardBadgesAsync(Guid eventId, Guid participantId, string actor)
    {
        var totalKm = await dbContext.Activities
            .Where(a => a.ParticipantId == participantId
                        && a.EventId == eventId
                        && a.Status == ActivityStatus.Approved
                        && a.CountsTowardTotal)
            .SumAsync(a => a.DistanceKm);

        var eligibleBadges = await dbContext.Badges
            .AsNoTracking()
            .Where(b => b.IsActive && b.ThresholdKm.HasValue && b.ThresholdKm.Value <= totalKm)
            .ToListAsync();

        if (eligibleBadges.Count == 0) return;

        var awardedBadgeIds = await dbContext.BadgeAwards
            .AsNoTracking()
            .Where(ba => ba.ParticipantId == participantId && ba.EventId == eventId)
            .Select(ba => ba.BadgeId)
            .ToHashSetAsync();

        foreach (var badge in eligibleBadges.Where(b => !awardedBadgeIds.Contains(b.Id)))
        {
            var award = new BadgeAward
            {
                ParticipantId = participantId,
                BadgeId = badge.Id,
                EventId = eventId,
                AwardedAt = DateTime.UtcNow,
                AwardedBy = actor,
                IsManual = false
            };

            dbContext.BadgeAwards.Add(award);

            dbContext.Notifications.Add(new Notification
            {
                ParticipantId = participantId,
                NotificationType = NotificationType.BadgeEarned,
                Title = "Badge Earned! 🏅",
                Message = $"You earned the '{badge.Name}' badge!",
                RelatedEntityType = "Badge",
                RelatedEntityId = badge.Id.ToString(),
                CreatedAt = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "BadgesChecked", "Participant", participantId.ToString(),
            eventId, null,
            $"TotalKm: {totalKm}, NewAwards: {eligibleBadges.Count(b => !awardedBadgeIds.Contains(b.Id))}");
    }

    public async Task GrantBadgeAsync(Guid eventId, Guid badgeId, Guid participantId, string actor)
    {
        var badge = await dbContext.Badges.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == badgeId)
            ?? throw new KeyNotFoundException($"Badge '{badgeId}' not found.");

        var participant = await dbContext.Participants.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == participantId && p.EventId == eventId)
            ?? throw new KeyNotFoundException($"Participant '{participantId}' not found in event '{eventId}'.");

        var alreadyAwarded = await dbContext.BadgeAwards
            .AnyAsync(ba => ba.ParticipantId == participantId
                            && ba.BadgeId == badgeId
                            && ba.EventId == eventId);

        if (alreadyAwarded)
            throw new InvalidOperationException($"Participant already has the '{badge.Name}' badge.");

        var award = new BadgeAward
        {
            ParticipantId = participantId,
            BadgeId = badgeId,
            EventId = eventId,
            AwardedAt = DateTime.UtcNow,
            AwardedBy = actor,
            IsManual = true
        };

        dbContext.BadgeAwards.Add(award);

        dbContext.Notifications.Add(new Notification
        {
            ParticipantId = participantId,
            NotificationType = NotificationType.BadgeEarned,
            Title = "Badge Earned! 🏅",
            Message = $"You were awarded the '{badge.Name}' badge!",
            RelatedEntityType = "Badge",
            RelatedEntityId = badgeId.ToString(),
            CreatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor, "BadgeGranted", "BadgeAward", award.Id.ToString(),
            eventId, null,
            JsonSerializer.Serialize(new { badgeId, badge.Name, participantId, IsManual = true }));
    }
}
