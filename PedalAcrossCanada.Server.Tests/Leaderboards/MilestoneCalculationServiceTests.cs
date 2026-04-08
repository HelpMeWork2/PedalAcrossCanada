using Microsoft.EntityFrameworkCore;
using PedalAcrossCanada.Server.Application.Services;
using PedalAcrossCanada.Server.Domain.Entities;
using PedalAcrossCanada.Server.Tests.Fakes;
using PedalAcrossCanada.Server.Tests.Helpers;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Tests.Leaderboards;

public sealed class MilestoneCalculationServiceTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory;
    private readonly FakeAuditService _auditService = new();

    public MilestoneCalculationServiceTests()
    {
        _dbFactory = new TestDbContextFactory();
    }

    public void Dispose() => _dbFactory.Dispose();

    private MilestoneCalculationService CreateService() =>
        new(_dbFactory.CreateContext(), _auditService);

    // ─── RecalculateMilestonesAsync ───────────────────────────────────────────

    [Fact]
    public async Task RecalculateMilestonesAsync_WhenTotalKmExceedsThreshold_AchievesMilestone()
    {
        var (eventId, milestoneId) = await SeedEventWithMilestoneAsync(thresholdKm: 100m);
        await SeedApprovedActivityAsync(eventId, km: 150m);

        await CreateService().RecalculateMilestonesAsync(eventId);

        await using var ctx = _dbFactory.CreateContext();
        var milestone = await ctx.Milestones.FindAsync(milestoneId);
        Assert.NotNull(milestone);
        Assert.NotNull(milestone.AchievedAt);
        Assert.Equal(150m, milestone.TotalKmAtAchievement);
    }

    [Fact]
    public async Task RecalculateMilestonesAsync_WhenBelowThreshold_DoesNotAchieveMilestone()
    {
        var (eventId, milestoneId) = await SeedEventWithMilestoneAsync(thresholdKm: 500m);
        await SeedApprovedActivityAsync(eventId, km: 100m);

        await CreateService().RecalculateMilestonesAsync(eventId);

        await using var ctx = _dbFactory.CreateContext();
        var milestone = await ctx.Milestones.FindAsync(milestoneId);
        Assert.NotNull(milestone);
        Assert.Null(milestone.AchievedAt);
    }

    [Fact]
    public async Task RecalculateMilestonesAsync_IsIdempotent()
    {
        var (eventId, milestoneId) = await SeedEventWithMilestoneAsync(thresholdKm: 100m);
        await SeedApprovedActivityAsync(eventId, km: 150m);

        var svc = CreateService();
        await svc.RecalculateMilestonesAsync(eventId);

        DateTime? firstAchievedAt;
        await using (var ctx = _dbFactory.CreateContext())
        {
            var m = await ctx.Milestones.FindAsync(milestoneId);
            firstAchievedAt = m!.AchievedAt;
        }

        // Second call should not change AchievedAt
        await CreateService().RecalculateMilestonesAsync(eventId);

        await using var ctx2 = _dbFactory.CreateContext();
        var milestone = await ctx2.Milestones.FindAsync(milestoneId);
        Assert.Equal(firstAchievedAt, milestone!.AchievedAt);
    }

    [Fact]
    public async Task RecalculateMilestonesAsync_WhenAchieved_CreatesNotificationsForActiveParticipants()
    {
        await using var setupCtx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        setupCtx.Events.Add(ev);
        var p = TestDataBuilder.CreateParticipant(ev.Id);
        setupCtx.Participants.Add(p);
        setupCtx.Milestones.Add(BuildMilestone(ev.Id, thresholdKm: 50m));
        setupCtx.Activities.Add(BuildApprovedActivity(ev.Id, p.Id, km: 100m));
        await setupCtx.SaveChangesAsync();

        await CreateService().RecalculateMilestonesAsync(ev.Id);

        await using var ctx = _dbFactory.CreateContext();
        var notificationCount = await ctx.Notifications
            .CountAsync(n => n.ParticipantId == p.Id
                             && n.NotificationType == NotificationType.MilestoneReached);
        Assert.Equal(1, notificationCount);
    }

    [Fact]
    public async Task RecalculateMilestonesAsync_WritesAuditEntry()
    {
        var (eventId, _) = await SeedEventWithMilestoneAsync(thresholdKm: 10m);
        await SeedApprovedActivityAsync(eventId, km: 50m);

        await CreateService().RecalculateMilestonesAsync(eventId);

        Assert.Single(_auditService.Entries);
        Assert.Equal("MilestoneAchieved", _auditService.Entries[0].Action);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<(Guid EventId, Guid MilestoneId)> SeedEventWithMilestoneAsync(decimal thresholdKm)
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ctx.Events.Add(ev);
        var m = BuildMilestone(ev.Id, thresholdKm);
        ctx.Milestones.Add(m);
        await ctx.SaveChangesAsync();
        return (ev.Id, m.Id);
    }

    private async Task SeedApprovedActivityAsync(Guid eventId, decimal km)
    {
        await using var ctx = _dbFactory.CreateContext();
        var participantId = await ctx.Participants
            .Where(p => p.EventId == eventId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (participantId == Guid.Empty)
        {
            var p = TestDataBuilder.CreateParticipant(eventId);
            ctx.Participants.Add(p);
            await ctx.SaveChangesAsync();
            participantId = p.Id;
        }

        ctx.Activities.Add(BuildApprovedActivity(eventId, participantId, km));
        await ctx.SaveChangesAsync();
    }

    private static Milestone BuildMilestone(Guid eventId, decimal thresholdKm) =>
        new()
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            StopName = $"Stop at {thresholdKm} km",
            OrderIndex = 0,
            CumulativeDistanceKm = thresholdKm
        };

    private static Activity BuildApprovedActivity(Guid eventId, Guid participantId, decimal km) =>
        new()
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            ParticipantId = participantId,
            ActivityDate = DateTime.UtcNow.AddDays(-1),
            DistanceKm = km,
            Source = ActivitySource.Manual,
            Status = ActivityStatus.Approved,
            CountsTowardTotal = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
}
