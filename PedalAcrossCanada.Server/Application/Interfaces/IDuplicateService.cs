using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Activities;

namespace PedalAcrossCanada.Server.Application.Interfaces;

public interface IDuplicateService
{
    /// <summary>
    /// Flags two activities as a duplicate pair.
    /// Sets <c>IsDuplicateFlagged = true</c> and <c>DuplicateOfActivityId</c> on both.
    /// No-op if they are already flagged against each other.
    /// </summary>
    Task FlagPairAsync(Guid firstActivityId, Guid secondActivityId, string actor);

    /// <summary>
    /// Returns all flagged duplicate pairs for an event (paginated).
    /// Each pair is returned once: the activity whose <c>DuplicateOfActivityId</c> is non-null
    /// is the "second"; the referenced activity is the "first".
    /// </summary>
    Task<PagedResult<DuplicatePairDto>> GetFlaggedPairsAsync(Guid eventId, int page, int pageSize);

    /// <summary>
    /// Resolves a flagged duplicate identified by the activity id of the "second" in a pair.
    /// KeepBoth  — clears flags on both, leaves both active.
    /// KeepFirst — invalidates second, recalculates totals, clears flags.
    /// KeepSecond — invalidates first, recalculates totals, clears flags.
    /// </summary>
    Task<DuplicatePairDto> ResolveAsync(Guid eventId, Guid activityId, DuplicateResolution resolution, string actor);
}
