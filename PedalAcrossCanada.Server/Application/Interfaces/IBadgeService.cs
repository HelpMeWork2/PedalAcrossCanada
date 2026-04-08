using PedalAcrossCanada.Shared.DTOs.Badges;

namespace PedalAcrossCanada.Server.Application.Interfaces;

public interface IBadgeService
{
    Task<IReadOnlyList<BadgeDto>> GetAllAsync(Guid eventId);
    Task<IReadOnlyList<BadgeAwardDto>> GetAwardsForParticipantAsync(Guid eventId, Guid participantId);
    Task CheckAndAwardBadgesAsync(Guid eventId, Guid participantId, string actor);
    Task GrantBadgeAsync(Guid eventId, Guid badgeId, Guid participantId, string actor);
}
