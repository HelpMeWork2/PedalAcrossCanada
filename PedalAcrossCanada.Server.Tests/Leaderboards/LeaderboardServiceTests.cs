using PedalAcrossCanada.Server.Application.Services;
using PedalAcrossCanada.Server.Domain.Entities;
using PedalAcrossCanada.Server.Tests.Helpers;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Tests.Leaderboards;

public sealed class LeaderboardServiceTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory;

    public LeaderboardServiceTests()
    {
        _dbFactory = new TestDbContextFactory();
    }

    public void Dispose() => _dbFactory.Dispose();

    private LeaderboardService CreateService() =>
        new(_dbFactory.CreateContext());

    // ─── Individual leaderboard ───────────────────────────────────────────────

    [Fact]
    public async Task GetIndividualLeaderboardAsync_RanksHigherKmFirst()
    {
        var (eventId, p1Id, p2Id) = await SeedTwoParticipantsAsync(km1: 100m, km2: 50m);

        var result = await CreateService().GetIndividualLeaderboardAsync(eventId, 1, 25);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(p1Id, result.Data[0].ParticipantId);
        Assert.Equal(1, result.Data[0].Rank);
        Assert.Equal(p2Id, result.Data[1].ParticipantId);
        Assert.Equal(2, result.Data[1].Rank);
    }

    [Fact]
    public async Task GetIndividualLeaderboardAsync_TieBreaksByRideCount()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ctx.Events.Add(ev);

        var joinTime = DateTime.UtcNow;
        var p1 = BuildParticipant(ev.Id, joinedAt: joinTime);
        var p2 = BuildParticipant(ev.Id, joinedAt: joinTime);
        ctx.Participants.AddRange(p1, p2);

        // Same km, p1 has 2 rides, p2 has 1 ride — p1 should rank higher
        ctx.Activities.AddRange(
            BuildApprovedActivity(ev.Id, p1.Id, 50m),
            BuildApprovedActivity(ev.Id, p1.Id, 50m),
            BuildApprovedActivity(ev.Id, p2.Id, 100m)
        );
        await ctx.SaveChangesAsync();

        var result = await CreateService().GetIndividualLeaderboardAsync(ev.Id, 1, 25);

        Assert.Equal(p1.Id, result.Data[0].ParticipantId);
        Assert.Equal(1, result.Data[0].Rank);
        Assert.Equal(2, result.Data[1].Rank);
    }

    [Fact]
    public async Task GetIndividualLeaderboardAsync_TieBreaksByJoinedAt()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ctx.Events.Add(ev);

        var earlier = DateTime.UtcNow.AddDays(-10);
        var later = DateTime.UtcNow.AddDays(-5);
        var p1 = BuildParticipant(ev.Id, joinedAt: earlier);
        var p2 = BuildParticipant(ev.Id, joinedAt: later);
        ctx.Participants.AddRange(p1, p2);

        // Same km, same ride count — p1 joined earlier so ranks higher
        ctx.Activities.AddRange(
            BuildApprovedActivity(ev.Id, p1.Id, 50m),
            BuildApprovedActivity(ev.Id, p2.Id, 50m)
        );
        await ctx.SaveChangesAsync();

        var result = await CreateService().GetIndividualLeaderboardAsync(ev.Id, 1, 25);

        Assert.Equal(p1.Id, result.Data[0].ParticipantId);
        Assert.Equal(1, result.Data[0].Rank);
        Assert.Equal(2, result.Data[1].Rank);
    }

    [Fact]
    public async Task GetIndividualLeaderboardAsync_AssignsSameRankForIdenticalStats()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ctx.Events.Add(ev);

        var joinTime = DateTime.UtcNow;
        var p1 = BuildParticipant(ev.Id, joinedAt: joinTime);
        var p2 = BuildParticipant(ev.Id, joinedAt: joinTime);
        ctx.Participants.AddRange(p1, p2);

        // Identical km, identical ride count, identical joinedAt → same rank
        ctx.Activities.AddRange(
            BuildApprovedActivity(ev.Id, p1.Id, 50m),
            BuildApprovedActivity(ev.Id, p2.Id, 50m)
        );
        await ctx.SaveChangesAsync();

        var result = await CreateService().GetIndividualLeaderboardAsync(ev.Id, 1, 25);

        Assert.Equal(result.Data[0].Rank, result.Data[1].Rank);
    }

    [Fact]
    public async Task GetIndividualLeaderboardAsync_ExcludesLeaderboardOptOutParticipants()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ctx.Events.Add(ev);

        var pIn = BuildParticipant(ev.Id, optIn: true);
        var pOut = BuildParticipant(ev.Id, optIn: false);
        ctx.Participants.AddRange(pIn, pOut);
        ctx.Activities.AddRange(
            BuildApprovedActivity(ev.Id, pIn.Id, 50m),
            BuildApprovedActivity(ev.Id, pOut.Id, 200m)
        );
        await ctx.SaveChangesAsync();

        var result = await CreateService().GetIndividualLeaderboardAsync(ev.Id, 1, 25);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(pIn.Id, result.Data[0].ParticipantId);
    }

    [Fact]
    public async Task GetIndividualLeaderboardAsync_ExcludesDeactivatedParticipants()
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ctx.Events.Add(ev);

        var pActive = BuildParticipant(ev.Id);
        var pInactive = BuildParticipant(ev.Id, status: ParticipantStatus.Inactive);
        ctx.Participants.AddRange(pActive, pInactive);
        ctx.Activities.AddRange(
            BuildApprovedActivity(ev.Id, pActive.Id, 50m),
            BuildApprovedActivity(ev.Id, pInactive.Id, 200m)
        );
        await ctx.SaveChangesAsync();

        var result = await CreateService().GetIndividualLeaderboardAsync(ev.Id, 1, 25);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(pActive.Id, result.Data[0].ParticipantId);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<(Guid EventId, Guid P1Id, Guid P2Id)> SeedTwoParticipantsAsync(
        decimal km1, decimal km2)
    {
        await using var ctx = _dbFactory.CreateContext();
        var ev = TestDataBuilder.CreateActiveEvent();
        ctx.Events.Add(ev);

        var joinTime = DateTime.UtcNow;
        var p1 = BuildParticipant(ev.Id, joinedAt: joinTime);
        var p2 = BuildParticipant(ev.Id, joinedAt: joinTime.AddSeconds(1));
        ctx.Participants.AddRange(p1, p2);

        ctx.Activities.AddRange(
            BuildApprovedActivity(ev.Id, p1.Id, km1),
            BuildApprovedActivity(ev.Id, p2.Id, km2)
        );
        await ctx.SaveChangesAsync();

        return (ev.Id, p1.Id, p2.Id);
    }

    private static Participant BuildParticipant(
        Guid eventId,
        DateTime? joinedAt = null,
        bool optIn = true,
        ParticipantStatus status = ParticipantStatus.Active) =>
        new()
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            UserId = Guid.NewGuid().ToString(),
            FirstName = "Test",
            LastName = "Rider",
            WorkEmail = $"{Guid.NewGuid():N}@example.com",
            DisplayName = $"Rider {Guid.NewGuid().ToString("N")[..4]}",
            Status = status,
            JoinedAt = joinedAt ?? DateTime.UtcNow,
            LeaderboardOptIn = optIn,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
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
