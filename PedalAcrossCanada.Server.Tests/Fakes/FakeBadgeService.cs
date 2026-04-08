using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Shared.DTOs.Badges;

namespace PedalAcrossCanada.Server.Tests.Fakes;

public sealed class FakeBadgeService : IBadgeService
{
    public List<(Guid EventId, Guid ParticipantId, string Actor)> CheckCalls { get; } = [];
    public List<(Guid EventId, Guid BadgeId, Guid ParticipantId, string Actor)> GrantCalls { get; } = [];

    public Task<IReadOnlyList<BadgeDto>> GetAllAsync(Guid eventId)
        => Task.FromResult<IReadOnlyList<BadgeDto>>([]);

    public Task<IReadOnlyList<BadgeAwardDto>> GetAwardsForParticipantAsync(Guid eventId, Guid participantId)
        => Task.FromResult<IReadOnlyList<BadgeAwardDto>>([]);

    public Task CheckAndAwardBadgesAsync(Guid eventId, Guid participantId, string actor)
    {
        CheckCalls.Add((eventId, participantId, actor));
        return Task.CompletedTask;
    }

    public Task GrantBadgeAsync(Guid eventId, Guid badgeId, Guid participantId, string actor)
    {
        GrantCalls.Add((eventId, badgeId, participantId, actor));
        return Task.CompletedTask;
    }
}
