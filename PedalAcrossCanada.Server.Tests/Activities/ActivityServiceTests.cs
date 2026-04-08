using PedalAcrossCanada.Server.Application.Services;
using PedalAcrossCanada.Server.Tests.Fakes;
using PedalAcrossCanada.Server.Tests.Helpers;
using PedalAcrossCanada.Shared.DTOs.Activities;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Tests.Activities;

public sealed class ActivityServiceTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory;
    private readonly FakeAuditService _auditService = new();
    private readonly FakeMilestoneCalculationService _milestoneCalculation = new();
    private readonly FakeBadgeService _badgeService = new();
    private readonly FakeDuplicateService _duplicateService = new();

    public ActivityServiceTests()
    {
        _dbFactory = new TestDbContextFactory();
    }

    public void Dispose() => _dbFactory.Dispose();

    private ActivityService CreateService() =>
        new(_dbFactory.CreateContext(), _auditService, _milestoneCalculation, _badgeService, _duplicateService);

    // ─── CreateAsync — validation ─────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WhenDistanceExceedsMax_Throws()
    {
        var (eventId, participantId) = await SeedEventAndParticipantAsync(maxSingleRideKm: 100m);

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().CreateAsync(eventId, participantId,
                BuildRequest(km: 150m), "user"));
    }

    [Fact]
    public async Task CreateAsync_WhenDistanceIsZero_Throws()
    {
        var (eventId, participantId) = await SeedEventAndParticipantAsync();

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().CreateAsync(eventId, participantId,
                BuildRequest(km: 0m), "user"));
    }

    [Fact]
    public async Task CreateAsync_WhenActivityDateIsInFuture_Throws()
    {
        var (eventId, participantId) = await SeedEventAndParticipantAsync();

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().CreateAsync(eventId, participantId,
                BuildRequest(date: DateTime.UtcNow.AddDays(1)), "user"));
    }

    [Fact]
    public async Task CreateAsync_WhenActivityDateBeforeEventStart_Throws()
    {
        var startDate = DateTime.UtcNow.AddDays(-30);
        var (eventId, participantId) = await SeedEventAndParticipantAsync(
            startDate: startDate, endDate: DateTime.UtcNow.AddDays(30));

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().CreateAsync(eventId, participantId,
                BuildRequest(date: startDate.AddDays(-1)), "user"));
    }

    [Fact]
    public async Task CreateAsync_WhenActivityDateAfterEventEnd_Throws()
    {
        var endDate = DateTime.UtcNow.AddDays(-1);
        var (eventId, participantId) = await SeedEventAndParticipantAsync(
            startDate: DateTime.UtcNow.AddDays(-60), endDate: endDate);

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().CreateAsync(eventId, participantId,
                BuildRequest(date: endDate.AddDays(1)), "user"));
    }

    [Fact]
    public async Task CreateAsync_WhenEventIsClosed_Throws()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ev.Status = EventStatus.Closed;
        ctx.Events.Add(ev);
        var p = TestDataBuilder.CreateParticipant(ev.Id);
        ctx.Participants.Add(p);
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().CreateAsync(ev.Id, p.Id, BuildRequest(), "user"));
    }

    [Fact]
    public async Task CreateAsync_WhenManualEntryDisabled_Throws()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ev.ManualEntryMode = ManualEntryMode.Disabled;
        ctx.Events.Add(ev);
        var p = TestDataBuilder.CreateParticipant(ev.Id);
        ctx.Participants.Add(p);
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().CreateAsync(ev.Id, p.Id, BuildRequest(), "user"));
    }

    // ─── CreateAsync — happy path ────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WithApprovalMode_CreatesPendingActivity()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ev.ManualEntryMode = ManualEntryMode.AllowedWithApproval;
        ctx.Events.Add(ev);
        var p = TestDataBuilder.CreateParticipant(ev.Id);
        ctx.Participants.Add(p);
        await ctx.SaveChangesAsync();

        var response = await CreateService().CreateAsync(ev.Id, p.Id, BuildRequest(), "user");

        Assert.Equal(ActivityStatus.Pending, response.Activity.Status);
        Assert.False(response.Activity.CountsTowardTotal);
    }

    [Fact]
    public async Task CreateAsync_WithDirectMode_CreatesApprovedActivity()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ev.ManualEntryMode = ManualEntryMode.AllowedWithoutApproval;
        ctx.Events.Add(ev);
        var p = TestDataBuilder.CreateParticipant(ev.Id);
        ctx.Participants.Add(p);
        await ctx.SaveChangesAsync();

        var response = await CreateService().CreateAsync(ev.Id, p.Id, BuildRequest(), "user");

        Assert.Equal(ActivityStatus.Approved, response.Activity.Status);
        Assert.True(response.Activity.CountsTowardTotal);
    }

    [Fact]
    public async Task CreateAsync_WhenDuplicateCandidateExists_ReturnsDuplicateWarning()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ev.ManualEntryMode = ManualEntryMode.AllowedWithApproval;
        ctx.Events.Add(ev);
        var p = TestDataBuilder.CreateParticipant(ev.Id);
        ctx.Participants.Add(p);

        var rideDate = DateTime.UtcNow.AddDays(-1);
        ctx.Activities.Add(new PedalAcrossCanada.Server.Domain.Entities.Activity
        {
            ParticipantId = p.Id,
            EventId = ev.Id,
            ActivityDate = rideDate,
            DistanceKm = 50m,
            Source = ActivitySource.Manual,
            Status = ActivityStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        // New activity: same date, within ±10% of 50 km → should trigger duplicate detection
        var response = await CreateService().CreateAsync(ev.Id, p.Id,
            BuildRequest(km: 52m, date: rideDate), "user");

        Assert.True(response.DuplicateWarning);
        Assert.NotNull(response.CandidateActivityId);
    }

    [Fact]
    public async Task CreateAsync_RoundsDistanceToTwoDecimalPlaces()
    {
        var (eventId, participantId) = await SeedEventAndParticipantAsync();

        var response = await CreateService().CreateAsync(eventId, participantId,
            BuildRequest(km: 23.456m), "user");

        Assert.Equal(23.46m, response.Activity.DistanceKm);
    }

    // ─── ApproveAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ApproveAsync_WhenPending_SetsApprovedAndCountsTowardTotal()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ctx.Events.Add(ev);
        var p = TestDataBuilder.CreateParticipant(ev.Id);
        ctx.Participants.Add(p);
        var activity = BuildActivity(ev.Id, p.Id, ActivityStatus.Pending);
        ctx.Activities.Add(activity);
        await ctx.SaveChangesAsync();

        var result = await CreateService().ApproveAsync(ev.Id, activity.Id, "admin");

        Assert.Equal(ActivityStatus.Approved, result.Status);
        Assert.True(result.CountsTowardTotal);
    }

    [Fact]
    public async Task ApproveAsync_WhenNotPending_Throws()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ctx.Events.Add(ev);
        var p = TestDataBuilder.CreateParticipant(ev.Id);
        ctx.Participants.Add(p);
        var activity = BuildActivity(ev.Id, p.Id, ActivityStatus.Approved);
        ctx.Activities.Add(activity);
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().ApproveAsync(ev.Id, activity.Id, "admin"));
    }

    [Fact]
    public async Task ApproveAsync_TriggersMilestoneCalculationAndBadgeCheck()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ctx.Events.Add(ev);
        var p = TestDataBuilder.CreateParticipant(ev.Id);
        ctx.Participants.Add(p);
        var activity = BuildActivity(ev.Id, p.Id, ActivityStatus.Pending);
        ctx.Activities.Add(activity);
        await ctx.SaveChangesAsync();

        await CreateService().ApproveAsync(ev.Id, activity.Id, "admin");

        Assert.Contains(ev.Id, _milestoneCalculation.RecalculatedEventIds);
        Assert.Contains(p.Id, _badgeService.CheckCalls.Select(c => c.ParticipantId));
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<(Guid EventId, Guid ParticipantId)> SeedEventAndParticipantAsync(
        decimal maxSingleRideKm = 300m,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent(
            startDate: startDate ?? DateTime.UtcNow.AddDays(-30),
            endDate: endDate ?? DateTime.UtcNow.AddDays(30));
        ev.MaxSingleRideKm = maxSingleRideKm;
        ev.ManualEntryMode = ManualEntryMode.AllowedWithApproval;
        ctx.Events.Add(ev);
        var p = TestDataBuilder.CreateParticipant(ev.Id);
        ctx.Participants.Add(p);
        await ctx.SaveChangesAsync();
        return (ev.Id, p.Id);
    }

    private static CreateActivityRequest BuildRequest(
        decimal km = 25m,
        DateTime? date = null) =>
        new()
        {
            DistanceKm = km,
            ActivityDate = date ?? DateTime.UtcNow.AddDays(-1)
        };

    private static PedalAcrossCanada.Server.Domain.Entities.Activity BuildActivity(
        Guid eventId, Guid participantId, ActivityStatus status) =>
        new()
        {
            EventId = eventId,
            ParticipantId = participantId,
            ActivityDate = DateTime.UtcNow.AddDays(-1),
            DistanceKm = 25m,
            Source = ActivitySource.Manual,
            Status = status,
            CountsTowardTotal = status == ActivityStatus.Approved,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
}
