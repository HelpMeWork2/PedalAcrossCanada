using Microsoft.EntityFrameworkCore;
using PedalAcrossCanada.Server.Application.Services;
using PedalAcrossCanada.Server.Tests.Fakes;
using PedalAcrossCanada.Server.Tests.Helpers;
using PedalAcrossCanada.Shared.DTOs.Activities;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Tests.Duplicates;

public sealed class DuplicateServiceTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory;
    private readonly FakeAuditService _auditService = new();
    private readonly FakeMilestoneCalculationService _milestoneCalculationService = new();
    private readonly FakeBadgeService _badgeService = new();

    public DuplicateServiceTests()
    {
        _dbFactory = new TestDbContextFactory();
    }

    public void Dispose() => _dbFactory.Dispose();

    private DuplicateService CreateService() =>
        new(_dbFactory.CreateContext(), _auditService, _milestoneCalculationService, _badgeService);

    // ─── FlagPairAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task FlagPairAsync_SetsFlagAndCrossReferenceOnBothActivities()
    {
        var (_, activityAId, activityBId) = await SeedPairAsync();

        await CreateService().FlagPairAsync(activityAId, activityBId, "admin");

        await using var ctx = _dbFactory.CreateContext();
        var a = await ctx.Activities.FindAsync(activityAId);
        var b = await ctx.Activities.FindAsync(activityBId);

        Assert.NotNull(a);
        Assert.NotNull(b);
        // First is flagged but does not hold the FK pointer
        Assert.True(a.IsDuplicateFlagged);
        Assert.Null(a.DuplicateOfActivityId);
        // Second is flagged and points back to first
        Assert.True(b.IsDuplicateFlagged);
        Assert.Equal(activityAId, b.DuplicateOfActivityId);
    }

    [Fact]
    public async Task FlagPairAsync_IsIdempotent_WhenCalledTwiceWithSamePair()
    {
        var (_, activityAId, activityBId) = await SeedPairAsync();
        var svc = CreateService();

        await svc.FlagPairAsync(activityAId, activityBId, "admin");
        await svc.FlagPairAsync(activityAId, activityBId, "admin"); // second call should be no-op

        await using var ctx = _dbFactory.CreateContext();
        var b = await ctx.Activities.FindAsync(activityBId);
        Assert.NotNull(b);
        Assert.True(b.IsDuplicateFlagged);
        Assert.Equal(activityAId, b.DuplicateOfActivityId);
    }

    [Fact]
    public async Task FlagPairAsync_WritesAuditEntry()
    {
        var (_, activityAId, activityBId) = await SeedPairAsync();

        await CreateService().FlagPairAsync(activityAId, activityBId, "admin");

        Assert.Single(_auditService.Entries);
        Assert.Equal("DuplicateFlagged", _auditService.Entries[0].Action);
    }

    // ─── GetFlaggedPairsAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetFlaggedPairsAsync_ReturnsFlaggedPairs()
    {
        var (eventId, activityAId, activityBId) = await SeedPairAsync();
        await CreateService().FlagPairAsync(activityAId, activityBId, "admin");

        var result = await CreateService().GetFlaggedPairsAsync(eventId, 1, 25);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Data);
    }

    [Fact]
    public async Task GetFlaggedPairsAsync_ReturnsEmpty_WhenNoPairsExist()
    {
        var (eventId, _, _) = await SeedPairAsync();

        var result = await CreateService().GetFlaggedPairsAsync(eventId, 1, 25);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task GetFlaggedPairsAsync_ReturnsCorrectFirstAndSecond()
    {
        var (eventId, activityAId, activityBId) = await SeedPairAsync();
        await CreateService().FlagPairAsync(activityAId, activityBId, "admin");

        var result = await CreateService().GetFlaggedPairsAsync(eventId, 1, 25);

        var pair = result.Data[0];
        Assert.Equal(activityAId, pair.First.Id);
        Assert.Equal(activityBId, pair.Second.Id);
    }

    // ─── ResolveAsync — KeepBoth ─────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_KeepBoth_ClearsFlagsOnBothActivities()
    {
        var (eventId, activityAId, activityBId) = await SeedPairAsync();
        await CreateService().FlagPairAsync(activityAId, activityBId, "admin");

        await CreateService().ResolveAsync(eventId, activityBId, DuplicateResolution.KeepBoth, "admin");

        await using var ctx = _dbFactory.CreateContext();
        var a = await ctx.Activities.FindAsync(activityAId);
        var b = await ctx.Activities.FindAsync(activityBId);

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.False(a.IsDuplicateFlagged);
        Assert.Null(a.DuplicateOfActivityId);
        Assert.False(b.IsDuplicateFlagged);
        Assert.Null(b.DuplicateOfActivityId);
    }

    [Fact]
    public async Task ResolveAsync_KeepBoth_DoesNotInvalidateEitherActivity()
    {
        var (eventId, activityAId, activityBId) = await SeedPairAsync();
        await CreateService().FlagPairAsync(activityAId, activityBId, "admin");

        await CreateService().ResolveAsync(eventId, activityBId, DuplicateResolution.KeepBoth, "admin");

        await using var ctx = _dbFactory.CreateContext();
        var a = await ctx.Activities.FindAsync(activityAId);
        var b = await ctx.Activities.FindAsync(activityBId);

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.NotEqual(ActivityStatus.Invalid, a.Status);
        Assert.NotEqual(ActivityStatus.Invalid, b.Status);
    }

    [Fact]
    public async Task ResolveAsync_KeepBoth_DoesNotTriggerRecalculation()
    {
        var (eventId, activityAId, activityBId) = await SeedPairAsync();
        await CreateService().FlagPairAsync(activityAId, activityBId, "admin");

        await CreateService().ResolveAsync(eventId, activityBId, DuplicateResolution.KeepBoth, "admin");

        Assert.Empty(_milestoneCalculationService.RecalculatedEventIds);
        Assert.Empty(_badgeService.CheckCalls);
    }

    // ─── ResolveAsync — KeepFirst ────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_KeepFirst_InvalidatesSecondActivity()
    {
        var (eventId, activityAId, activityBId) = await SeedPairAsync();
        await CreateService().FlagPairAsync(activityAId, activityBId, "admin");

        await CreateService().ResolveAsync(eventId, activityBId, DuplicateResolution.KeepFirst, "admin");

        await using var ctx = _dbFactory.CreateContext();
        var a = await ctx.Activities.FindAsync(activityAId);
        var b = await ctx.Activities.FindAsync(activityBId);

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.NotEqual(ActivityStatus.Invalid, a.Status);
        Assert.True(a.CountsTowardTotal);
        Assert.Equal(ActivityStatus.Invalid, b.Status);
        Assert.False(b.CountsTowardTotal);
    }

    [Fact]
    public async Task ResolveAsync_KeepFirst_ClearsFlagsOnBothActivities()
    {
        var (eventId, activityAId, activityBId) = await SeedPairAsync();
        await CreateService().FlagPairAsync(activityAId, activityBId, "admin");

        await CreateService().ResolveAsync(eventId, activityBId, DuplicateResolution.KeepFirst, "admin");

        await using var ctx = _dbFactory.CreateContext();
        var a = await ctx.Activities.FindAsync(activityAId);
        var b = await ctx.Activities.FindAsync(activityBId);

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.False(a.IsDuplicateFlagged);
        Assert.False(b.IsDuplicateFlagged);
    }

    [Fact]
    public async Task ResolveAsync_KeepFirst_TriggersRecalculation()
    {
        var (eventId, activityAId, activityBId) = await SeedPairAsync();
        await CreateService().FlagPairAsync(activityAId, activityBId, "admin");

        await CreateService().ResolveAsync(eventId, activityBId, DuplicateResolution.KeepFirst, "admin");

        Assert.Contains(eventId, _milestoneCalculationService.RecalculatedEventIds);
        Assert.Single(_badgeService.CheckCalls);
    }

    // ─── ResolveAsync — KeepSecond ───────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_KeepSecond_InvalidatesFirstActivity()
    {
        var (eventId, activityAId, activityBId) = await SeedPairAsync();
        await CreateService().FlagPairAsync(activityAId, activityBId, "admin");

        await CreateService().ResolveAsync(eventId, activityBId, DuplicateResolution.KeepSecond, "admin");

        await using var ctx = _dbFactory.CreateContext();
        var a = await ctx.Activities.FindAsync(activityAId);
        var b = await ctx.Activities.FindAsync(activityBId);

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal(ActivityStatus.Invalid, a.Status);
        Assert.False(a.CountsTowardTotal);
        Assert.NotEqual(ActivityStatus.Invalid, b.Status);
        Assert.True(b.CountsTowardTotal);
    }

    [Fact]
    public async Task ResolveAsync_KeepSecond_TriggersRecalculation()
    {
        var (eventId, activityAId, activityBId) = await SeedPairAsync();
        await CreateService().FlagPairAsync(activityAId, activityBId, "admin");

        await CreateService().ResolveAsync(eventId, activityBId, DuplicateResolution.KeepSecond, "admin");

        Assert.Contains(eventId, _milestoneCalculationService.RecalculatedEventIds);
        Assert.Single(_badgeService.CheckCalls);
    }

    // ─── ResolveAsync — audit + guard ────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_WritesAuditEntry()
    {
        var (eventId, activityAId, activityBId) = await SeedPairAsync();
        await CreateService().FlagPairAsync(activityAId, activityBId, "admin");
        _auditService.Entries.Clear();

        await CreateService().ResolveAsync(eventId, activityBId, DuplicateResolution.KeepFirst, "admin");

        Assert.Single(_auditService.Entries);
        Assert.Equal("DuplicateResolved", _auditService.Entries[0].Action);
    }

    [Fact]
    public async Task ResolveAsync_Throws_WhenActivityIsNotFlagged()
    {
        var (eventId, _, activityBId) = await SeedPairAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().ResolveAsync(eventId, activityBId, DuplicateResolution.KeepFirst, "admin"));
    }

    [Fact]
    public async Task ResolveAsync_Throws_WhenActivityNotFound()
    {
        var (eventId, _, _) = await SeedPairAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => CreateService().ResolveAsync(eventId, Guid.NewGuid(), DuplicateResolution.KeepFirst, "admin"));
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>Seeds an event, a participant, and two approved activities with
    /// identical dates and distances so they qualify as duplicates.</summary>
    private async Task<(Guid EventId, Guid ActivityAId, Guid ActivityBId)> SeedPairAsync(
        decimal distanceKm = 30m)
    {
        await using var ctx = _dbFactory.CreateContext();

        var evt = TestDataBuilder.CreateActiveEvent();
        var participant = TestDataBuilder.CreateParticipant(evt.Id);

        var activityDate = evt.StartDate.AddDays(1);

        var activityA = CreateApprovedActivity(participant.Id, evt.Id, distanceKm, activityDate);
        var activityB = CreateApprovedActivity(participant.Id, evt.Id, distanceKm, activityDate);

        ctx.Events.Add(evt);
        ctx.Participants.Add(participant);
        ctx.Activities.AddRange(activityA, activityB);
        await ctx.SaveChangesAsync();

        return (evt.Id, activityA.Id, activityB.Id);
    }

    private static Server.Domain.Entities.Activity CreateApprovedActivity(
        Guid participantId,
        Guid eventId,
        decimal distanceKm,
        DateTime activityDate)
    {
        return new Server.Domain.Entities.Activity
        {
            Id = Guid.NewGuid(),
            ParticipantId = participantId,
            EventId = eventId,
            ActivityDate = activityDate,
            DistanceKm = distanceKm,
            RideType = RideType.Other,
            Source = ActivitySource.Manual,
            Status = ActivityStatus.Approved,
            CountsTowardTotal = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
