using Microsoft.EntityFrameworkCore;
using PedalAcrossCanada.Server.Application.Services;
using PedalAcrossCanada.Server.Tests.Fakes;
using PedalAcrossCanada.Server.Tests.Helpers;
using PedalAcrossCanada.Shared.DTOs.Events;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Tests.Events;

public sealed class EventServiceTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory;
    private readonly FakeAuditService _auditService = new();

    public EventServiceTests()
    {
        _dbFactory = new TestDbContextFactory();
    }

    public void Dispose() => _dbFactory.Dispose();

    private EventService CreateService() =>
        new(_dbFactory.CreateContext(), _auditService);

    // ─── CreateAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WhenValid_ReturnsEventWithDraftStatus()
    {
        var request = BuildCreateRequest(
            startDate: DateTime.UtcNow.AddDays(1),
            endDate: DateTime.UtcNow.AddDays(30));

        var result = await CreateService().CreateAsync(request, "admin");

        Assert.Equal(EventStatus.Draft, result.Status);
        Assert.Equal(request.Name, result.Name);
    }

    [Fact]
    public async Task CreateAsync_WhenEndDateBeforeStartDate_Throws()
    {
        var request = BuildCreateRequest(
            startDate: DateTime.UtcNow.AddDays(10),
            endDate: DateTime.UtcNow.AddDays(1));

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().CreateAsync(request, "admin"));
    }

    [Fact]
    public async Task CreateAsync_WritesAuditEntry()
    {
        var request = BuildCreateRequest();

        await CreateService().CreateAsync(request, "admin");

        Assert.Single(_auditService.Entries);
        Assert.Equal("EventCreated", _auditService.Entries[0].Action);
    }

    // ─── ActivateAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task ActivateAsync_WhenEventHasNoMilestones_Throws()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateDraftEvent();
        ctx.Events.Add(ev);
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().ActivateAsync(ev.Id, "admin"));
    }

    [Fact]
    public async Task ActivateAsync_WhenAnotherEventIsAlreadyActive_Throws()
    {
        await using var ctx = _dbFactory.CreateContext();
        var existingActive = TestDataBuilder.CreateActiveEvent();
        var draft = TestDataBuilder.CreateDraftEvent();
        ctx.Events.AddRange(existingActive, draft);
        ctx.Milestones.Add(BuildMilestone(draft.Id));
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().ActivateAsync(draft.Id, "admin"));
    }

    [Fact]
    public async Task ActivateAsync_WhenDraftWithMilestones_SetsStatusActive()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateDraftEvent();
        ctx.Events.Add(ev);
        ctx.Milestones.Add(BuildMilestone(ev.Id));
        await ctx.SaveChangesAsync();

        var result = await CreateService().ActivateAsync(ev.Id, "admin");

        Assert.Equal(EventStatus.Active, result.Status);
    }

    [Fact]
    public async Task ActivateAsync_WhenEventIsNotDraft_Throws()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ctx.Events.Add(ev);
        ctx.Milestones.Add(BuildMilestone(ev.Id));
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().ActivateAsync(ev.Id, "admin"));
    }

    // ─── CloseAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CloseAsync_WhenEventIsActive_SetsStatusClosed()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ctx.Events.Add(ev);
        await ctx.SaveChangesAsync();

        var result = await CreateService().CloseAsync(ev.Id, "admin");

        Assert.Equal(EventStatus.Closed, result.Status);
    }

    [Fact]
    public async Task CloseAsync_WhenEventIsNotActive_Throws()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateDraftEvent();
        ctx.Events.Add(ev);
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().CloseAsync(ev.Id, "admin"));
    }

    // ─── ArchiveAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ArchiveAsync_WhenEventIsClosed_SetsStatusArchived()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ev.Status = EventStatus.Closed;
        ctx.Events.Add(ev);
        await ctx.SaveChangesAsync();

        var result = await CreateService().ArchiveAsync(ev.Id, "admin");

        Assert.Equal(EventStatus.Archived, result.Status);
    }

    // ─── UpdateAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_WhenEndDateBeforeStartDate_Throws()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateDraftEvent();
        ctx.Events.Add(ev);
        await ctx.SaveChangesAsync();

        var request = new UpdateEventRequest
        {
            Name = ev.Name,
            StartDate = DateTime.UtcNow.AddDays(10),
            EndDate = DateTime.UtcNow.AddDays(1),
            RouteDistanceKm = ev.RouteDistanceKm,
            MaxSingleRideKm = ev.MaxSingleRideKm
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().UpdateAsync(ev.Id, request, "admin"));
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static CreateEventRequest BuildCreateRequest(
        DateTime? startDate = null,
        DateTime? endDate = null) =>
        new()
        {
            Name = "Test Event",
            StartDate = startDate ?? DateTime.UtcNow.AddDays(1),
            EndDate = endDate ?? DateTime.UtcNow.AddDays(60),
            RouteDistanceKm = 3757m,
            MaxSingleRideKm = 300m
        };

    private static PedalAcrossCanada.Server.Domain.Entities.Milestone BuildMilestone(Guid eventId) =>
        new()
        {
            EventId = eventId,
            StopName = "Montreal",
            OrderIndex = 0,
            CumulativeDistanceKm = 1m
        };
}
