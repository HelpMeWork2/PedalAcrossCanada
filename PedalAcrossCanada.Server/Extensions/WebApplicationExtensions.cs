namespace PedalAcrossCanada.Server.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Runs EF Core migrations and seeds initial data.
    /// Populated in Phase 2 when AppDbContext is introduced.
    /// </summary>
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        // Phase 2: migrate database, seed badges
        await Task.CompletedTask;
    }
}
