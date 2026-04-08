using System.Globalization;
using CsvHelper;
using PedalAcrossCanada.Server.Application.Services;
using PedalAcrossCanada.Server.Tests.Helpers;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Tests.Reports;

public sealed class ReportServiceTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory;

    public ReportServiceTests()
    {
        _dbFactory = new TestDbContextFactory();
    }

    public void Dispose() => _dbFactory.Dispose();

    private ReportService CreateService() => new(_dbFactory.CreateContext());

    // ─── Participants report ─────────────────────────────────────────────────

    [Fact]
    public async Task GetParticipantsReportAsync_ReturnsNonEmptyBytes_WhenParticipantsExist()
    {
        var (eventId, _) = await SeedParticipantWithActivityAsync();

        var bytes = await CreateService().GetParticipantsReportAsync(eventId);

        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task GetParticipantsReportAsync_ContainsExpectedColumns()
    {
        var (eventId, _) = await SeedParticipantWithActivityAsync();

        var bytes = await CreateService().GetParticipantsReportAsync(eventId);
        var rows = ParseCsv(bytes);

        Assert.NotEmpty(rows);
        Assert.True(rows[0].ContainsKey("DisplayName"), "Missing column: DisplayName");
        Assert.True(rows[0].ContainsKey("WorkEmail"), "Missing column: WorkEmail");
        Assert.True(rows[0].ContainsKey("Status"), "Missing column: Status");
        Assert.True(rows[0].ContainsKey("TotalKm"), "Missing column: TotalKm");
        Assert.True(rows[0].ContainsKey("RideCount"), "Missing column: RideCount");
    }

    [Fact]
    public async Task GetParticipantsReportAsync_FiltersTeam_ReturnsOnlyMatchingParticipants()
    {
        await using var ctx = _dbFactory.CreateContext();
        var evt = TestDataBuilder.CreateActiveEvent();
        var team1 = new Server.Domain.Entities.Team
        {
            Id = Guid.NewGuid(), EventId = evt.Id, Name = "Alpha",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var team2 = new Server.Domain.Entities.Team
        {
            Id = Guid.NewGuid(), EventId = evt.Id, Name = "Beta",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var p1 = TestDataBuilder.CreateParticipant(evt.Id);
        p1.TeamId = team1.Id;
        var p2 = TestDataBuilder.CreateParticipant(evt.Id);
        p2.TeamId = team2.Id;

        ctx.Events.Add(evt);
        ctx.Teams.AddRange(team1, team2);
        ctx.Participants.AddRange(p1, p2);
        await ctx.SaveChangesAsync();

        var bytes = await CreateService().GetParticipantsReportAsync(evt.Id, teamId: team1.Id);
        var rows = ParseCsv(bytes);

        Assert.Single(rows);
        Assert.Equal(p1.DisplayName, rows[0]["DisplayName"]);
    }

    // ─── Activities report ───────────────────────────────────────────────────

    [Fact]
    public async Task GetActivitiesReportAsync_ContainsExpectedColumns()
    {
        var (eventId, _) = await SeedParticipantWithActivityAsync();

        var bytes = await CreateService().GetActivitiesReportAsync(eventId);
        var rows = ParseCsv(bytes);

        Assert.NotEmpty(rows);
        Assert.True(rows[0].ContainsKey("ParticipantDisplayName"), "Missing column: ParticipantDisplayName");
        Assert.True(rows[0].ContainsKey("DistanceKm"), "Missing column: DistanceKm");
        Assert.True(rows[0].ContainsKey("Source"), "Missing column: Source");
        Assert.True(rows[0].ContainsKey("Status"), "Missing column: Status");
        Assert.True(rows[0].ContainsKey("CountsTowardTotal"), "Missing column: CountsTowardTotal");
    }

    [Fact]
    public async Task GetActivitiesReportAsync_FiltersDateRange_ReturnsOnlyMatchingActivities()
    {
        await using var ctx = _dbFactory.CreateContext();
        var evt = TestDataBuilder.CreateActiveEvent();
        var participant = TestDataBuilder.CreateParticipant(evt.Id);

        var activityInRange = CreateApprovedActivity(participant.Id, evt.Id,
            date: new DateTime(2025, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var activityOutOfRange = CreateApprovedActivity(participant.Id, evt.Id,
            date: new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc));

        ctx.Events.Add(evt);
        ctx.Participants.Add(participant);
        ctx.Activities.AddRange(activityInRange, activityOutOfRange);
        await ctx.SaveChangesAsync();

        var start = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var bytes = await CreateService().GetActivitiesReportAsync(evt.Id, startDate: start, endDate: end);
        var rows = ParseCsv(bytes);

        Assert.Single(rows);
    }

    // ─── Team totals report ──────────────────────────────────────────────────

    [Fact]
    public async Task GetTeamTotalsReportAsync_ContainsExpectedColumns()
    {
        await using var ctx = _dbFactory.CreateContext();
        var evt = TestDataBuilder.CreateActiveEvent();
        var team = new Server.Domain.Entities.Team
        {
            Id = Guid.NewGuid(), EventId = evt.Id, Name = "Rockets",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        ctx.Events.Add(evt);
        ctx.Teams.Add(team);
        await ctx.SaveChangesAsync();

        var bytes = await CreateService().GetTeamTotalsReportAsync(evt.Id);
        var rows = ParseCsv(bytes);

        Assert.Single(rows);
        Assert.True(rows[0].ContainsKey("TeamName"), "Missing column: TeamName");
        Assert.True(rows[0].ContainsKey("TotalKm"), "Missing column: TotalKm");
        Assert.True(rows[0].ContainsKey("ActiveParticipantCount"), "Missing column: ActiveParticipantCount");
    }

    [Fact]
    public async Task GetTeamTotalsReportAsync_SumsApprovedActivitiesOnly()
    {
        await using var ctx = _dbFactory.CreateContext();
        var evt = TestDataBuilder.CreateActiveEvent();
        var team = new Server.Domain.Entities.Team
        {
            Id = Guid.NewGuid(), EventId = evt.Id, Name = "Rockets",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var participant = TestDataBuilder.CreateParticipant(evt.Id);
        participant.TeamId = team.Id;

        var approved = CreateApprovedActivity(participant.Id, evt.Id, distanceKm: 50m);
        var pending = CreateApprovedActivity(participant.Id, evt.Id, distanceKm: 30m);
        pending.Status = ActivityStatus.Pending;
        pending.CountsTowardTotal = false;

        ctx.Events.Add(evt);
        ctx.Teams.Add(team);
        ctx.Participants.Add(participant);
        ctx.Activities.AddRange(approved, pending);
        await ctx.SaveChangesAsync();

        var bytes = await CreateService().GetTeamTotalsReportAsync(evt.Id);
        var rows = ParseCsv(bytes);

        Assert.Single(rows);
        Assert.Equal(50m, decimal.Parse(rows[0]["TotalKm"], System.Globalization.CultureInfo.InvariantCulture));
    }

    // ─── Milestones report ───────────────────────────────────────────────────

    [Fact]
    public async Task GetMilestonesReportAsync_ContainsExpectedColumns()
    {
        await using var ctx = _dbFactory.CreateContext();
        var evt = TestDataBuilder.CreateActiveEvent();
        var milestone = new Server.Domain.Entities.Milestone
        {
            Id = Guid.NewGuid(), EventId = evt.Id, StopName = "Montreal",
            OrderIndex = 1, CumulativeDistanceKm = 100m
        };

        ctx.Events.Add(evt);
        ctx.Milestones.Add(milestone);
        await ctx.SaveChangesAsync();

        var bytes = await CreateService().GetMilestonesReportAsync(evt.Id);
        var rows = ParseCsv(bytes);

        Assert.Single(rows);
        Assert.True(rows[0].ContainsKey("StopName"), "Missing column: StopName");
        Assert.True(rows[0].ContainsKey("CumulativeDistanceKm"), "Missing column: CumulativeDistanceKm");
        Assert.True(rows[0].ContainsKey("IsAchieved"), "Missing column: IsAchieved");
    }

    // ─── Badge awards report ─────────────────────────────────────────────────

    [Fact]
    public async Task GetBadgeAwardsReportAsync_ContainsExpectedColumns()
    {
        await using var ctx = _dbFactory.CreateContext();
        var evt = TestDataBuilder.CreateActiveEvent();
        var participant = TestDataBuilder.CreateParticipant(evt.Id);
        var badge = new Server.Domain.Entities.Badge
        {
            Id = Guid.NewGuid(), Name = $"Test Badge {Guid.NewGuid():N}", ThresholdKm = 100m,
            IsDefault = false, IsActive = true
        };
        var award = new Server.Domain.Entities.BadgeAward
        {
            Id = Guid.NewGuid(), EventId = evt.Id, ParticipantId = participant.Id,
            BadgeId = badge.Id, AwardedAt = DateTime.UtcNow, IsManual = false
        };

        ctx.Events.Add(evt);
        ctx.Participants.Add(participant);
        ctx.Badges.Add(badge);
        ctx.BadgeAwards.Add(award);
        await ctx.SaveChangesAsync();

        var bytes = await CreateService().GetBadgeAwardsReportAsync(evt.Id);
        var rows = ParseCsv(bytes);

        Assert.Single(rows);
        Assert.True(rows[0].ContainsKey("BadgeName"), "Missing column: BadgeName");
        Assert.True(rows[0].ContainsKey("ParticipantDisplayName"), "Missing column: ParticipantDisplayName");
        Assert.True(rows[0].ContainsKey("IsManual"), "Missing column: IsManual");
    }

    // ─── Executive summary ───────────────────────────────────────────────────

    [Fact]
    public async Task GetExecutiveSummaryReportAsync_ContainsHeadlineSection()
    {
        var (eventId, _) = await SeedParticipantWithActivityAsync();

        var bytes = await CreateService().GetExecutiveSummaryReportAsync(eventId);
        var rows = ParseCsv(bytes);

        Assert.NotEmpty(rows);
        Assert.True(rows[0].ContainsKey("Section"), "Missing column: Section");
        Assert.True(rows[0].ContainsKey("Label"), "Missing column: Label");
        Assert.True(rows[0].ContainsKey("Value"), "Missing column: Value");
        Assert.Contains(rows, r => r["Section"] == "Headline" && r["Label"] == "Total Km Ridden");
    }

    [Fact]
    public async Task GetExecutiveSummaryReportAsync_ReturnsEmptyBytes_WhenEventNotFound()
    {
        var bytes = await CreateService().GetExecutiveSummaryReportAsync(Guid.NewGuid());

        Assert.Empty(bytes);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<(Guid EventId, Guid ParticipantId)> SeedParticipantWithActivityAsync(
        decimal distanceKm = 42m)
    {
        await using var ctx = _dbFactory.CreateContext();
        var evt = TestDataBuilder.CreateActiveEvent();
        var participant = TestDataBuilder.CreateParticipant(evt.Id);
        var activity = CreateApprovedActivity(participant.Id, evt.Id, distanceKm: distanceKm);

        ctx.Events.Add(evt);
        ctx.Participants.Add(participant);
        ctx.Activities.Add(activity);
        await ctx.SaveChangesAsync();

        return (evt.Id, participant.Id);
    }

    private static Server.Domain.Entities.Activity CreateApprovedActivity(
        Guid participantId,
        Guid eventId,
        decimal distanceKm = 25m,
        DateTime? date = null)
    {
        return new Server.Domain.Entities.Activity
        {
            Id = Guid.NewGuid(),
            ParticipantId = participantId,
            EventId = eventId,
            ActivityDate = date ?? DateTime.UtcNow.AddDays(-1),
            DistanceKm = distanceKm,
            RideType = RideType.Other,
            Source = ActivitySource.Manual,
            Status = ActivityStatus.Approved,
            CountsTowardTotal = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static List<Dictionary<string, string>> ParseCsv(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var reader = new StreamReader(ms);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        var records = new List<Dictionary<string, string>>();
        csv.Read();
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? [];

        while (csv.Read())
        {
            var row = new Dictionary<string, string>();
            foreach (var header in headers)
                row[header] = csv.GetField(header) ?? string.Empty;
            records.Add(row);
        }

        return records;
    }
}
