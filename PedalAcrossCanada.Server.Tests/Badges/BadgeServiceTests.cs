using Microsoft.EntityFrameworkCore;
using PedalAcrossCanada.Server.Application.Services;
using PedalAcrossCanada.Server.Domain.Entities;
using PedalAcrossCanada.Server.Tests.Fakes;
using PedalAcrossCanada.Server.Tests.Helpers;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Tests.Badges;

public sealed class BadgeServiceTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory;
    private readonly FakeAuditService _auditService = new();

    public BadgeServiceTests()
    {
        _dbFactory = new TestDbContextFactory();
    }

    public void Dispose() => _dbFactory.Dispose();

    private BadgeService CreateService() =>
        new(_dbFactory.CreateContext(), _auditService);

    // ─── CheckAndAwardBadgesAsync ─────────────────────────────────────────────

    [Fact]
    public async Task CheckAndAwardBadgesAsync_WhenParticipantMeetsThreshold_AwardsBadge()
    {
        var (eventId, participantId, badgeId) = await SeedAsync(thresholdKm: 50m, activityKm: 100m);

        await CreateService().CheckAndAwardBadgesAsync(eventId, participantId, "system");

        await using var ctx = _dbFactory.CreateContext();
        var awarded = await ctx.BadgeAwards
            .AnyAsync(ba => ba.ParticipantId == participantId && ba.BadgeId == badgeId);
        Assert.True(awarded);
    }

    [Fact]
    public async Task CheckAndAwardBadgesAsync_WhenBelowThreshold_DoesNotAwardBadge()
    {
        var (eventId, participantId, badgeId) = await SeedAsync(thresholdKm: 500m, activityKm: 100m);

        await CreateService().CheckAndAwardBadgesAsync(eventId, participantId, "system");

        await using var ctx = _dbFactory.CreateContext();
        var awarded = await ctx.BadgeAwards
            .AnyAsync(ba => ba.ParticipantId == participantId && ba.BadgeId == badgeId);
        Assert.False(awarded);
    }

    [Fact]
    public async Task CheckAndAwardBadgesAsync_IsIdempotent_DoesNotDoubleAward()
    {
        var (eventId, participantId, badgeId) = await SeedAsync(thresholdKm: 50m, activityKm: 100m);

        await CreateService().CheckAndAwardBadgesAsync(eventId, participantId, "system");
        await CreateService().CheckAndAwardBadgesAsync(eventId, participantId, "system");

        await using var ctx = _dbFactory.CreateContext();
        var awardCount = await ctx.BadgeAwards
            .CountAsync(ba => ba.ParticipantId == participantId && ba.BadgeId == badgeId);
        Assert.Equal(1, awardCount);
    }

    [Fact]
    public async Task CheckAndAwardBadgesAsync_AwardsMultipleEligibleBadges()
    {
        await using var setupCtx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        setupCtx.Events.Add(ev);
        var p = TestDataBuilder.CreateParticipant(ev.Id);
        setupCtx.Participants.Add(p);

        var badge50 = BuildBadge(thresholdKm: 50m, name: "Test 50 km");
        var badge100 = BuildBadge(thresholdKm: 100m, name: "Test 100 km");
        setupCtx.Badges.AddRange(badge50, badge100);

        setupCtx.Activities.Add(BuildApprovedActivity(ev.Id, p.Id, km: 150m));
        await setupCtx.SaveChangesAsync();

        await CreateService().CheckAndAwardBadgesAsync(ev.Id, p.Id, "system");

        await using var ctx = _dbFactory.CreateContext();
        var awardCount = await ctx.BadgeAwards
            .CountAsync(ba => ba.ParticipantId == p.Id
                && ba.EventId == ev.Id
                && (ba.BadgeId == badge50.Id || ba.BadgeId == badge100.Id));
        Assert.Equal(2, awardCount);
    }

    [Fact]
    public async Task CheckAndAwardBadgesAsync_CreatesNotificationForEachNewBadge()
    {
        var (eventId, participantId, badgeId) = await SeedAsync(thresholdKm: 50m, activityKm: 100m);

        await CreateService().CheckAndAwardBadgesAsync(eventId, participantId, "system");

        await using var ctx = _dbFactory.CreateContext();
        var notificationCount = await ctx.Notifications
            .CountAsync(n => n.ParticipantId == participantId
                             && n.NotificationType == NotificationType.BadgeEarned
                             && n.RelatedEntityId == badgeId.ToString());
        Assert.Equal(1, notificationCount);
    }

    [Fact]
    public async Task CheckAndAwardBadgesAsync_OnlyCountsApprovedActivitiesCountingTowardTotal()
    {
        await using var setupCtx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        setupCtx.Events.Add(ev);
        var p = TestDataBuilder.CreateParticipant(ev.Id);
        setupCtx.Participants.Add(p);
        var badge = BuildBadge(thresholdKm: 100m);
        setupCtx.Badges.Add(badge);

        // 80 km pending + 80 km approved but not counting → 0 km total → badge should not award
        setupCtx.Activities.AddRange(
            BuildActivity(ev.Id, p.Id, km: 80m, status: ActivityStatus.Pending, counts: false),
            BuildActivity(ev.Id, p.Id, km: 80m, status: ActivityStatus.Approved, counts: false)
        );
        await setupCtx.SaveChangesAsync();

        await CreateService().CheckAndAwardBadgesAsync(ev.Id, p.Id, "system");

        await using var ctx = _dbFactory.CreateContext();
        var awarded = await ctx.BadgeAwards
            .AnyAsync(ba => ba.ParticipantId == p.Id && ba.BadgeId == badge.Id);
        Assert.False(awarded);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<(Guid EventId, Guid ParticipantId, Guid BadgeId)> SeedAsync(
        decimal thresholdKm, decimal activityKm)
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ctx.Events.Add(ev);
        var p = TestDataBuilder.CreateParticipant(ev.Id);
        ctx.Participants.Add(p);
        var badge = BuildBadge(thresholdKm: thresholdKm);
        ctx.Badges.Add(badge);
        ctx.Activities.Add(BuildApprovedActivity(ev.Id, p.Id, activityKm));
        await ctx.SaveChangesAsync();
        return (ev.Id, p.Id, badge.Id);
    }

    private static Badge BuildBadge(decimal thresholdKm, string name = "Test Badge") =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            ThresholdKm = thresholdKm,
            IsDefault = false,
            IsActive = true,
            SortOrder = 0
        };

    private static Activity BuildApprovedActivity(Guid eventId, Guid participantId, decimal km) =>
        BuildActivity(eventId, participantId, km, ActivityStatus.Approved, counts: true);

    private static Activity BuildActivity(
        Guid eventId, Guid participantId, decimal km,
        ActivityStatus status, bool counts) =>
        new()
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            ParticipantId = participantId,
            ActivityDate = DateTime.UtcNow.AddDays(-1),
            DistanceKm = km,
            Source = ActivitySource.Manual,
            Status = status,
            CountsTowardTotal = counts,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
}
