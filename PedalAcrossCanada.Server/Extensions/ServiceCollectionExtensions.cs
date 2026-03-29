namespace PedalAcrossCanada.Server.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all application services. Populate this in subsequent phases
    /// rather than adding registrations directly to Program.cs.
    /// </summary>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Phase 2: EF Core + AppDbContext
        // Phase 3: Identity, JWT, AuthService
        // Phase 4+: domain services registered here

        return services;
    }
}
