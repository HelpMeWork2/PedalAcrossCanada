using Microsoft.EntityFrameworkCore;
using PedalAcrossCanada.Server.Application.Services;
using PedalAcrossCanada.Server.Tests.Fakes;
using PedalAcrossCanada.Server.Tests.Helpers;
using PedalAcrossCanada.Shared.DTOs.Participants;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Tests.Participants;

public sealed class ParticipantServiceTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory;
    private readonly FakeAuditService _auditService = new();

    public ParticipantServiceTests()
    {
        _dbFactory = new TestDbContextFactory();
    }

    public void Dispose() => _dbFactory.Dispose();

    private ParticipantService CreateService() =>
        new(_dbFactory.CreateContext(), _auditService);

    // ─── CreateAsync — duplicate prevention ──────────────────────────────────

    [Fact]
    public async Task CreateAsync_WhenDuplicateEmail_Throws()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ctx.Events.Add(ev);
        var existing = TestDataBuilder.CreateParticipant(ev.Id);
        ctx.Participants.Add(existing);
        await ctx.SaveChangesAsync();

        var request = BuildRequest(email: existing.WorkEmail);

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().CreateAsync(ev.Id, request, "new-user-id", "admin"));
    }

    [Fact]
    public async Task CreateAsync_WhenSameUserIdAlreadyRegistered_Throws()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ctx.Events.Add(ev);
        var userId = Guid.NewGuid().ToString();
        var existing = TestDataBuilder.CreateParticipant(ev.Id, userId: userId);
        ctx.Participants.Add(existing);
        await ctx.SaveChangesAsync();

        var request = BuildRequest();

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().CreateAsync(ev.Id, request, userId, "admin"));
    }

    [Fact]
    public async Task CreateAsync_WhenEventIsClosed_Throws()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ev.Status = EventStatus.Closed;
        ctx.Events.Add(ev);
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().CreateAsync(ev.Id, BuildRequest(), "user-id", "admin"));
    }

    [Fact]
    public async Task CreateAsync_WhenValid_CreatesActiveParticipant()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ctx.Events.Add(ev);
        await ctx.SaveChangesAsync();

        var result = await CreateService().CreateAsync(ev.Id, BuildRequest(), "new-user", "admin");

        Assert.Equal(ParticipantStatus.Active, result.Status);
        Assert.Equal("Jane", result.FirstName);
    }

    [Fact]
    public async Task CreateAsync_WritesAuditEntry()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ctx.Events.Add(ev);
        await ctx.SaveChangesAsync();

        await CreateService().CreateAsync(ev.Id, BuildRequest(), "new-user", "admin");

        Assert.Single(_auditService.Entries);
        Assert.Equal("ParticipantCreated", _auditService.Entries[0].Action);
    }

    // ─── DeactivateAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task DeactivateAsync_WhenActive_SetsStatusInactive()
    {
        var (eventId, participantId) = await SeedParticipantAsync();

        var result = await CreateService().DeactivateAsync(eventId, participantId, "admin");

        Assert.Equal(ParticipantStatus.Inactive, result.Status);
    }

    [Fact]
    public async Task DeactivateAsync_WhenAlreadyInactive_Throws()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ctx.Events.Add(ev);
        var p = TestDataBuilder.CreateParticipant(ev.Id);
        p.Status = ParticipantStatus.Inactive;
        ctx.Participants.Add(p);
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().DeactivateAsync(ev.Id, p.Id, "admin"));
    }

    // ─── ReactivateAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ReactivateAsync_WhenInactive_SetsStatusActive()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ctx.Events.Add(ev);
        var p = TestDataBuilder.CreateParticipant(ev.Id);
        p.Status = ParticipantStatus.Inactive;
        ctx.Participants.Add(p);
        await ctx.SaveChangesAsync();

        var result = await CreateService().ReactivateAsync(ev.Id, p.Id, "admin");

        Assert.Equal(ParticipantStatus.Active, result.Status);
    }

    // ─── ChangeTeamAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ChangeTeamAsync_WhenTeamExists_UpdatesTeamAndRecordsHistory()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ctx.Events.Add(ev);
        var p = TestDataBuilder.CreateParticipant(ev.Id);
        ctx.Participants.Add(p);
        var team = new PedalAcrossCanada.Server.Domain.Entities.Team
        {
            Id = Guid.NewGuid(),
            EventId = ev.Id,
            Name = "Team A",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        ctx.Teams.Add(team);
        await ctx.SaveChangesAsync();

        var result = await CreateService().ChangeTeamAsync(ev.Id, p.Id, team.Id, "admin");

        Assert.Equal(team.Id, result.TeamId);

        await using var verifyCtx = _dbFactory.CreateContext();
        var historyCount = await verifyCtx.TeamHistories
            .CountAsync(th => th.ParticipantId == p.Id && th.TeamId == team.Id);
        Assert.Equal(1, historyCount);
    }

    [Fact]
    public async Task ChangeTeamAsync_WhenTeamNotInEvent_Throws()
    {
        var (eventId, participantId) = await SeedParticipantAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => CreateService().ChangeTeamAsync(eventId, participantId, Guid.NewGuid(), "admin"));
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<(Guid EventId, Guid ParticipantId)> SeedParticipantAsync()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ctx.Events.Add(ev);
        var p = TestDataBuilder.CreateParticipant(ev.Id);
        ctx.Participants.Add(p);
        await ctx.SaveChangesAsync();
        return (ev.Id, p.Id);
    }

    private static CreateParticipantRequest BuildRequest(string? email = null) =>
        new()
        {
            FirstName = "Jane",
            LastName = "Doe",
            WorkEmail = email ?? $"{Guid.NewGuid():N}@example.com",
            DisplayName = "JaneDoe"
        };
}
