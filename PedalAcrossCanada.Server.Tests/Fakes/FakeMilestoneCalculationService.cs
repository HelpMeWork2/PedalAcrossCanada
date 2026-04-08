using PedalAcrossCanada.Server.Application.Interfaces;

namespace PedalAcrossCanada.Server.Tests.Fakes;

public sealed class FakeMilestoneCalculationService : IMilestoneCalculationService
{
    public List<Guid> RecalculatedEventIds { get; } = [];

    public Task RecalculateMilestonesAsync(Guid eventId)
    {
        RecalculatedEventIds.Add(eventId);
        return Task.CompletedTask;
    }
}
