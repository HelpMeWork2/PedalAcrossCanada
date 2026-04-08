using PedalAcrossCanada.Server.Application.Services;
using PedalAcrossCanada.Server.Tests.Fakes;
using PedalAcrossCanada.Server.Tests.Helpers;
using PedalAcrossCanada.Shared.DTOs.Milestones;

namespace PedalAcrossCanada.Server.Tests.Events;

public sealed class MilestoneServiceTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory;
    private readonly FakeAuditService _auditService = new();

    public MilestoneServiceTests()
    {
        _dbFactory = new TestDbContextFactory();
    }

    public void Dispose() => _dbFactory.Dispose();

    private MilestoneService CreateService() =>
        new(_dbFactory.CreateContext(), _auditService);

    // ─── CreateAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WhenEventIsNotDraft_Throws()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ctx.Events.Add(ev);
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().CreateAsync(ev.Id, BuildRequest(orderIndex: 0, km: 100m), "admin"));
    }

    [Fact]
    public async Task CreateAsync_WhenDraftAndValid_CreatesMilestone()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateDraftEvent();
        ctx.Events.Add(ev);
        await ctx.SaveChangesAsync();

        var result = await CreateService().CreateAsync(ev.Id, BuildRequest(orderIndex: 0, km: 100m), "admin");

        Assert.Equal(100m, result.CumulativeDistanceKm);
        Assert.Equal("Stop A", result.StopName);
    }

    [Fact]
    public async Task CreateAsync_WhenDistanceIsNotAscending_Throws()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateDraftEvent();
        ctx.Events.Add(ev);
        ctx.Milestones.Add(new PedalAcrossCanada.Server.Domain.Entities.Milestone
        {
            EventId = ev.Id,
            StopName = "First",
            OrderIndex = 0,
            CumulativeDistanceKm = 500m
        });
        await ctx.SaveChangesAsync();

        // Trying to add a stop with lower km than an existing one
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().CreateAsync(ev.Id, BuildRequest(orderIndex: 1, km: 200m), "admin"));
    }

    [Fact]
    public async Task CreateAsync_WritesAuditEntry()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateDraftEvent();
        ctx.Events.Add(ev);
        await ctx.SaveChangesAsync();

        await CreateService().CreateAsync(ev.Id, BuildRequest(orderIndex: 0, km: 100m), "admin");

        Assert.Single(_auditService.Entries);
        Assert.Equal("MilestoneCreated", _auditService.Entries[0].Action);
    }

    // ─── UpdateAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_WhenEventIsActiveAndDistanceChanged_Throws()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ctx.Events.Add(ev);
        var milestone = new PedalAcrossCanada.Server.Domain.Entities.Milestone
        {
            EventId = ev.Id,
            StopName = "Stop",
            OrderIndex = 0,
            CumulativeDistanceKm = 100m
        };
        ctx.Milestones.Add(milestone);
        await ctx.SaveChangesAsync();

        var request = new UpdateMilestoneRequest
        {
            StopName = "Stop",
            OrderIndex = 0,
            CumulativeDistanceKm = 200m // changed
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().UpdateAsync(ev.Id, milestone.Id, request, "admin"));
    }

    [Fact]
    public async Task UpdateAsync_WhenEventIsActiveAndOrderChanged_Throws()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ctx.Events.Add(ev);
        var milestone = new PedalAcrossCanada.Server.Domain.Entities.Milestone
        {
            EventId = ev.Id,
            StopName = "Stop",
            OrderIndex = 0,
            CumulativeDistanceKm = 100m
        };
        ctx.Milestones.Add(milestone);
        await ctx.SaveChangesAsync();

        var request = new UpdateMilestoneRequest
        {
            StopName = "Stop",
            OrderIndex = 5, // changed
            CumulativeDistanceKm = 100m
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().UpdateAsync(ev.Id, milestone.Id, request, "admin"));
    }

    [Fact]
    public async Task UpdateAsync_WhenEventIsActiveAndOnlyDescriptionChanged_Succeeds()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ctx.Events.Add(ev);
        var milestone = new PedalAcrossCanada.Server.Domain.Entities.Milestone
        {
            EventId = ev.Id,
            StopName = "Stop",
            OrderIndex = 0,
            CumulativeDistanceKm = 100m
        };
        ctx.Milestones.Add(milestone);
        await ctx.SaveChangesAsync();

        var request = new UpdateMilestoneRequest
        {
            StopName = "Updated Stop",
            OrderIndex = 0,
            CumulativeDistanceKm = 100m,
            Description = "New description"
        };

        var result = await CreateService().UpdateAsync(ev.Id, milestone.Id, request, "admin");

        Assert.Equal("Updated Stop", result.StopName);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static CreateMilestoneRequest BuildRequest(int orderIndex, decimal km) =>
        new()
        {
            StopName = "Stop A",
            OrderIndex = orderIndex,
            CumulativeDistanceKm = km
        };
}
