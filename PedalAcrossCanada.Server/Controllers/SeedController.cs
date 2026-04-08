using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PedalAcrossCanada.Server.Domain.Entities;
using PedalAcrossCanada.Server.Infrastructure.Data;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Controllers;

/// <summary>
/// Development-only endpoints for seeding initial data.
/// All actions require Admin role and are only available in the Development environment.
/// </summary>
[Authorize(Roles = "Admin")]
public class SeedController(AppDbContext dbContext, IWebHostEnvironment env) : ApiControllerBase
{
    private static readonly (string StopName, int OrderIndex, decimal CumulativeDistanceKm)[] MontrealToCalgaryStops =
    [
        ("Montreal, QC",        0,   0m),
        ("Ottawa, ON",          1,   198m),
        ("Kingston, ON",        2,   362m),
        ("Peterborough, ON",    3,   448m),
        ("Oshawa, ON",          4,   504m),
        ("Toronto, ON",         5,   543m),
        ("Hamilton, ON",        6,   594m),
        ("Guelph, ON",          7,   625m),
        ("Waterloo, ON",        8,   652m),
        ("Barrie, ON",          9,   737m),
        ("Sudbury, ON",         10,  996m),
        ("Elliot Lake, ON",     11,  1076m),
        ("Sault Ste. Marie, ON",12,  1192m),
        ("Marathon, ON",        13,  1506m),
        ("Thunder Bay, ON",     14,  1713m),
        ("Dryden, ON",          15,  2016m),
        ("Kenora, ON",          16,  2115m),
        ("Winnipeg, MB",        17,  2294m),
        ("Brandon, MB",         18,  2472m),
        ("Regina, SK",          19,  2773m),
        ("Moose Jaw, SK",       20,  2836m),
        ("Saskatoon, SK",       21,  3028m),
        ("Lloydminster, SK/AB", 22,  3224m),
        ("Edmonton, AB",        23,  3508m),
        ("Calgary, AB",         24,  3757m),
    ];

    /// <summary>
    /// Seeds the 25 Montreal-to-Calgary route milestones for the active event.
    /// Dev environment only. Skips stops that already exist (idempotent).
    /// </summary>
    [HttpPost("milestones")]
    [ProducesResponseType(typeof(SeedMilestonesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SeedMilestonesAsync()
    {
        if (!env.IsDevelopment())
            return NotFound();

        var activeEvent = await dbContext.Events
            .FirstOrDefaultAsync(e => e.Status == EventStatus.Active || e.Status == EventStatus.Draft);

        if (activeEvent is null)
            return NotFound(new ProblemDetails
            {
                Title = "No event found.",
                Detail = "Create a Draft or Active event before seeding milestones."
            });

        if (activeEvent.Status == EventStatus.Active)
        {
            var hasExisting = await dbContext.Milestones
                .AnyAsync(m => m.EventId == activeEvent.Id);

            if (hasExisting)
                return Conflict(new ProblemDetails
                {
                    Title = "Cannot seed into an Active event that already has milestones.",
                    Detail = "Milestones cannot be modified on an Active event."
                });
        }

        var existingOrders = await dbContext.Milestones
            .Where(m => m.EventId == activeEvent.Id)
            .Select(m => m.OrderIndex)
            .ToHashSetAsync();

        var added = 0;
        foreach (var (stopName, orderIndex, cumulativeKm) in MontrealToCalgaryStops)
        {
            if (existingOrders.Contains(orderIndex))
                continue;

            dbContext.Milestones.Add(new Milestone
            {
                EventId = activeEvent.Id,
                StopName = stopName,
                OrderIndex = orderIndex,
                CumulativeDistanceKm = cumulativeKm,
                Description = $"Virtual stop at {stopName} — {cumulativeKm:N0} km from Montreal."
            });
            added++;
        }

        await dbContext.SaveChangesAsync();

        return Ok(new SeedMilestonesResponse(activeEvent.Id, activeEvent.Name, added));
    }
}

public record SeedMilestonesResponse(Guid EventId, string EventName, int MilestonesAdded);
