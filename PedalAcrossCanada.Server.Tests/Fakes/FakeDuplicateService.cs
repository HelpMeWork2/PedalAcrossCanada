using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Activities;

namespace PedalAcrossCanada.Server.Tests.Fakes;

public sealed class FakeDuplicateService : IDuplicateService
{
    public List<(Guid FirstId, Guid SecondId, string Actor)> FlagCalls { get; } = [];
    public List<(Guid EventId, Guid ActivityId, DuplicateResolution Resolution, string Actor)> ResolveCalls { get; } = [];

    public Task FlagPairAsync(Guid firstActivityId, Guid secondActivityId, string actor)
    {
        FlagCalls.Add((firstActivityId, secondActivityId, actor));
        return Task.CompletedTask;
    }

    public Task<PagedResult<DuplicatePairDto>> GetFlaggedPairsAsync(Guid eventId, int page, int pageSize)
        => Task.FromResult(PagedResult<DuplicatePairDto>.Empty(page, pageSize));

    public Task<DuplicatePairDto> ResolveAsync(Guid eventId, Guid activityId, DuplicateResolution resolution, string actor)
    {
        ResolveCalls.Add((eventId, activityId, resolution, actor));
        return Task.FromResult(new DuplicatePairDto
        {
            First = new ActivityDto(),
            Second = new ActivityDto()
        });
    }
}
