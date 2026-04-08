using System.Text;
using Hangfire;
using Hangfire.InMemory;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Server.Application.Services;
using PedalAcrossCanada.Server.Configuration;
using PedalAcrossCanada.Server.Domain.Entities;
using PedalAcrossCanada.Server.Infrastructure.Data;

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
        // EF Core
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("Default")));

        // Identity
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        // JWT
        var jwtSection = configuration.GetSection(JwtSettings.SectionName);
        services.Configure<JwtSettings>(jwtSection);
        var jwtSettings = jwtSection.Get<JwtSettings>()
            ?? throw new InvalidOperationException("JWT settings are not configured.");

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSettings.Key)),
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
            });

        services.AddAuthorization();

        // Data Protection (for Strava token encryption)
        services.AddDataProtection();

        // Strava configuration
        services.Configure<StravaSettings>(configuration.GetSection(StravaSettings.SectionName));

        // HttpClientFactory for external API calls
        services.AddHttpClient("Strava");

        // Hangfire (in-memory storage for dev; swap to SqlServer in prod)
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseInMemoryStorage());
        services.AddHangfireServer(options =>
        {
            options.WorkerCount = 2;
            options.Queues = ["strava", "default"];
        });

        // Application services
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IMilestoneService, MilestoneService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<IParticipantService, ParticipantService>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddSingleton<ITokenEncryptionService, TokenEncryptionService>();
        services.AddScoped<IStravaTokenService, StravaTokenService>();
        services.AddScoped<IStravaApiClient, StravaApiClient>();
        services.AddScoped<IStravaSyncService, StravaSyncService>();
        services.AddScoped<ILeaderboardService, LeaderboardService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IMilestoneCalculationService, MilestoneCalculationService>();
        services.AddScoped<IBadgeService, BadgeService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IDuplicateService, DuplicateService>();

        return services;
    }
}
