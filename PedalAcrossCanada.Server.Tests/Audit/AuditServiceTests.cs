using PedalAcrossCanada.Server.Application.Services;
using PedalAcrossCanada.Server.Domain.Entities;
using PedalAcrossCanada.Server.Tests.Helpers;

namespace PedalAcrossCanada.Server.Tests.Audit;

public sealed class AuditServiceTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory;

    public AuditServiceTests()
    {
        _dbFactory = new TestDbContextFactory();
    }

    public void Dispose() => _dbFactory.Dispose();

    private AuditService CreateService() => new(_dbFactory.CreateContext());

    // ─── GetPagedAsync – filtering ───────────────────────────────────────────

    [Fact]
    public async Task GetPagedAsync_ReturnsAllEntries_WhenNoFiltersApplied()
    {
        await SeedAuditLogsAsync(3);

        var result = await CreateService().GetPagedAsync();

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Data.Count);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersEntityType_ReturnsMatchingEntries()
    {
        await SeedAuditLogsAsync(1, entityType: "Activity");
        await SeedAuditLogsAsync(2, entityType: "Event");

        var result = await CreateService().GetPagedAsync(entityType: "Activity");

        Assert.Equal(1, result.TotalCount);
        Assert.All(result.Data, e => Assert.Equal("Activity", e.EntityType));
    }

    [Fact]
    public async Task GetPagedAsync_FiltersEntityId_ReturnsMatchingEntry()
    {
        var targetId = Guid.NewGuid().ToString();
        await SeedAuditLogsAsync(2, entityType: "Participant");
        await SeedAuditLogsAsync(1, entityType: "Participant", entityId: targetId);

        var result = await CreateService().GetPagedAsync(entityId: targetId);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(targetId, result.Data[0].EntityId);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersActor_ReturnsMatchingEntries()
    {
        var actorId = "user-abc-123";
        await SeedAuditLogsAsync(3, actor: "other-user");
        await SeedAuditLogsAsync(2, actor: actorId);

        var result = await CreateService().GetPagedAsync(actor: actorId);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Data, e => Assert.Equal(actorId, e.Actor));
    }

    [Fact]
    public async Task GetPagedAsync_FiltersStartDate_ExcludesOlderEntries()
    {
        var old = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var recent = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        await SeedAuditLogsAsync(1, timestamp: old);
        await SeedAuditLogsAsync(1, timestamp: recent);

        var cutoff = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = await CreateService().GetPagedAsync(startDate: cutoff);

        Assert.Equal(1, result.TotalCount);
        Assert.True(result.Data[0].Timestamp >= cutoff);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersEndDate_ExcludesNewerEntries()
    {
        var old = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var future = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await SeedAuditLogsAsync(1, timestamp: old);
        await SeedAuditLogsAsync(1, timestamp: future);

        var cutoff = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = await CreateService().GetPagedAsync(endDate: cutoff);

        Assert.Equal(1, result.TotalCount);
        Assert.True(result.Data[0].Timestamp <= cutoff);
    }

    // ─── GetPagedAsync – pagination ──────────────────────────────────────────

    [Fact]
    public async Task GetPagedAsync_ReturnsCorrectPage_WhenPaginationApplied()
    {
        await SeedAuditLogsAsync(10);

        var result = await CreateService().GetPagedAsync(page: 2, pageSize: 3);

        Assert.Equal(10, result.TotalCount);
        Assert.Equal(3, result.Data.Count);
        Assert.Equal(2, result.Page);
        Assert.Equal(4, result.TotalPages);
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsDescendingTimestampOrder()
    {
        var now = DateTime.UtcNow;
        await SeedAuditLogsAsync(1, timestamp: now.AddDays(-2));
        await SeedAuditLogsAsync(1, timestamp: now.AddDays(-1));
        await SeedAuditLogsAsync(1, timestamp: now);

        var result = await CreateService().GetPagedAsync();

        Assert.Equal(3, result.Data.Count);
        Assert.True(result.Data[0].Timestamp >= result.Data[1].Timestamp);
        Assert.True(result.Data[1].Timestamp >= result.Data[2].Timestamp);
    }

    // ─── GetPagedAsync – DTO mapping ─────────────────────────────────────────

    [Fact]
    public async Task GetPagedAsync_MapsAllDtoFields_Correctly()
    {
        var entityId = Guid.NewGuid().ToString();
        await SeedAuditLogsAsync(1,
            actor: "user-xyz",
            action: "CreateActivity",
            entityType: "Activity",
            entityId: entityId,
            beforeSummary: "{\"old\":true}",
            afterSummary: "{\"new\":true}");

        var result = await CreateService().GetPagedAsync(entityId: entityId);

        Assert.Single(result.Data);
        var dto = result.Data[0];
        Assert.Equal("user-xyz", dto.Actor);
        Assert.Equal("CreateActivity", dto.Action);
        Assert.Equal("Activity", dto.EntityType);
        Assert.Equal(entityId, dto.EntityId);
        Assert.Equal("{\"old\":true}", dto.BeforeSummary);
        Assert.Equal("{\"new\":true}", dto.AfterSummary);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task SeedAuditLogsAsync(
        int count,
        string actor = "system",
        string action = "Create",
        string entityType = "Event",
        string? entityId = null,
        string? beforeSummary = null,
        string? afterSummary = null,
        DateTime? timestamp = null)
    {
        await using var ctx = _dbFactory.CreateContext();
        for (var i = 0; i < count; i++)
        {
            ctx.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                Actor = actor,
                Action = action,
                EntityType = entityType,
                EntityId = entityId ?? Guid.NewGuid().ToString(),
                Timestamp = timestamp ?? DateTime.UtcNow,
                BeforeSummary = beforeSummary,
                AfterSummary = afterSummary
            });
        }
        await ctx.SaveChangesAsync();
    }
}
