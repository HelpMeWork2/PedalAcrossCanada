using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Server.Configuration;
using PedalAcrossCanada.Server.Domain.Entities;
using PedalAcrossCanada.Server.Infrastructure.Data;
using PedalAcrossCanada.Shared.DTOs.Strava;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Controllers;

[Route("api/[controller]")]
public class StravaController(
    IStravaTokenService stravaTokenService,
    IStravaSyncService stravaSyncService,
    IStravaApiClient stravaApiClient,
    IAuditService auditService,
    UserManager<ApplicationUser> userManager,
    IOptions<StravaSettings> stravaSettings,
    AppDbContext dbContext) : ApiControllerBase
{
    [HttpGet("auth-url")]
    [Authorize]
    [ProducesResponseType(typeof(StravaAuthUrlDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StravaAuthUrlDto>> GetAuthUrl([FromQuery] Guid eventId)
    {
        var participant = await GetParticipantForCurrentUserAsync(eventId);
        var url = stravaTokenService.BuildAuthorizationUrl(participant.Id);
        return Ok(new StravaAuthUrlDto { AuthorizationUrl = url });
    }

    [HttpPost("callback")]
    [Authorize]
    [ProducesResponseType(typeof(StravaStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StravaStatusDto>> Callback(
        [FromQuery] string code,
        [FromQuery] Guid state)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest(new ProblemDetails { Title = "Missing authorization code." });
        }

        var actor = GetUserId();
        var result = await stravaTokenService.ExchangeCodeAsync(code, state, actor);
        return Ok(result);
    }

    [HttpPost("disconnect")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Disconnect([FromQuery] Guid eventId)
    {
        var actor = GetUserId();
        var participant = await GetParticipantForCurrentUserAsync(eventId);
        await stravaTokenService.DisconnectAsync(participant.Id, actor);
        return NoContent();
    }

    [HttpGet("status")]
    [Authorize]
    [ProducesResponseType(typeof(StravaStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StravaStatusDto>> GetStatus([FromQuery] Guid eventId)
    {
        var participant = await GetParticipantForCurrentUserAsync(eventId);
        var status = await stravaTokenService.GetStatusAsync(participant.Id);
        return Ok(status);
    }

    [HttpPost("sync")]
    [Authorize]
    [ProducesResponseType(typeof(StravaSyncResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StravaSyncResultDto>> ManualSync([FromQuery] Guid eventId)
    {
        var actor = GetUserId();
        var participant = await GetParticipantForCurrentUserAsync(eventId);
        var result = await stravaSyncService.SyncParticipantAsync(participant.Id, actor);
        return Ok(result);
    }

    [HttpPost("sync-all")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(BulkStravaSyncResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BulkStravaSyncResultDto>> SyncAll([FromQuery] Guid eventId)
    {
        var actor = GetUserId();
        var result = await stravaSyncService.SyncAllForEventAsync(eventId, actor);
        return Ok(result);
    }

    [HttpPost("sync-club-activities")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ClubActivitySyncResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClubActivitySyncResultDto>> SyncClubActivities([FromQuery] Guid eventId)
    {
        var settings = stravaSettings.Value;
        if (string.IsNullOrWhiteSpace(settings.ClubId))
            return BadRequest(new ProblemDetails { Title = "Strava ClubId is not configured." });

        var actor = GetUserId();
        var participant = await GetParticipantForCurrentUserAsync(eventId);
        var tokenData = await stravaTokenService.GetTokenDataAsync(participant.Id)
            ?? throw new InvalidOperationException("Admin Strava account is not connected. Connect your Strava account first.");

        var accessToken = await EnsureValidAccessTokenAsync(participant.Id, tokenData);
        var result = await stravaSyncService.SyncClubActivitiesAsync(eventId, accessToken, settings.ClubId, actor);
        return Ok(result);
    }

    [HttpGet("club-members")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<StravaClubMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<StravaClubMemberDto>>> GetClubMembers([FromQuery] Guid eventId)
    {
        var settings = stravaSettings.Value;
        if (string.IsNullOrWhiteSpace(settings.ClubId))
            return BadRequest(new ProblemDetails { Title = "Strava ClubId is not configured." });

        var participant = await GetParticipantForCurrentUserAsync(eventId);
        var tokenData = await stravaTokenService.GetTokenDataAsync(participant.Id)
            ?? throw new InvalidOperationException("Admin Strava account is not connected. Connect your Strava account first.");

        var accessToken = await EnsureValidAccessTokenAsync(participant.Id, tokenData);

        var members = await stravaApiClient.GetClubMembersAsync(accessToken, settings.ClubId);

        var registeredAthleteIds = await dbContext.ExternalConnections
            .AsNoTracking()
            .Where(ec => ec.Provider == "Strava"
                && ec.ConnectionStatus == ConnectionStatus.Connected
                && ec.Participant.EventId == eventId)
            .Select(ec => ec.ExternalAthleteId)
            .ToListAsync();

        var registeredSet = new HashSet<string>(registeredAthleteIds);
        var emailPattern = settings.EmailDomainPattern;

        var dtos = members.Select(m => new StravaClubMemberDto
        {
            AthleteId = m.AthleteId,
            FirstName = m.FirstName,
            LastName = m.LastName,
            ProfilePictureUrl = m.ProfilePictureUrl,
            IsOwner = m.IsOwner,
            IsAdmin = m.IsAdmin,
            AlreadyRegistered = registeredSet.Contains(m.AthleteId.ToString()),
            SuggestedEmail = GenerateSuggestedEmail(m.FirstName, m.LastName, emailPattern)
        }).ToList();

        return Ok(dtos);
    }

    [HttpPost("import-club-members")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ImportClubMembersResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImportClubMembersResultDto>> ImportClubMembers(
        [FromQuery] Guid eventId,
        [FromBody] ImportClubMembersRequest request)
    {
        var ev = await dbContext.Events.FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event with id '{eventId}' not found.");

        if (ev.Status is EventStatus.Closed or EventStatus.Archived)
            return Conflict(new ProblemDetails { Title = "Cannot import participants into a Closed or Archived event." });

        var actor = GetUserId();
        var result = new ImportClubMembersResultDto { TotalRequested = request.Members.Count };

        foreach (var member in request.Members)
        {
            try
            {
                // Check if email already registered for event
                var existingParticipant = await dbContext.Participants
                    .AnyAsync(p => p.EventId == eventId
                        && p.WorkEmail == member.WorkEmail
                        && p.Status == ParticipantStatus.Active);

                if (existingParticipant)
                {
                    result.SkippedAlreadyRegistered++;
                    continue;
                }

                // Find or create Identity user
                var user = await userManager.FindByEmailAsync(member.WorkEmail);
                if (user is null)
                {
                    user = new ApplicationUser
                    {
                        UserName = member.WorkEmail,
                        Email = member.WorkEmail
                    };

                    var randomPassword = GenerateRandomPassword();
                    var createResult = await userManager.CreateAsync(user, randomPassword);
                    if (!createResult.Succeeded)
                    {
                        var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                        result.Errors.Add($"{member.WorkEmail}: {errors}");
                        continue;
                    }

                    await userManager.AddToRoleAsync(user, "Participant");
                }

                // Check if this user already has an active participant for the event
                var userAlreadyParticipant = await dbContext.Participants
                    .AnyAsync(p => p.EventId == eventId
                        && p.UserId == user.Id
                        && p.Status == ParticipantStatus.Active);

                if (userAlreadyParticipant)
                {
                    result.SkippedAlreadyRegistered++;
                    continue;
                }

                // Create participant
                var participant = new Participant
                {
                    EventId = eventId,
                    UserId = user.Id,
                    FirstName = member.FirstName,
                    LastName = member.LastName,
                    WorkEmail = member.WorkEmail,
                    DisplayName = member.DisplayName,
                    Status = ParticipantStatus.Active,
                    JoinedAt = DateTime.UtcNow,
                    LeaderboardOptIn = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                dbContext.Participants.Add(participant);
                await dbContext.SaveChangesAsync();

                await auditService.LogAsync(
                    actor, "ParticipantImported", "Participant", participant.Id.ToString(),
                    eventId, null, System.Text.Json.JsonSerializer.Serialize(participant));

                result.Imported++;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{member.WorkEmail}: {ex.Message}");
            }
        }

        return Ok(result);
    }

    private async Task<string> EnsureValidAccessTokenAsync(Guid participantId, StravaTokenData tokenData)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (tokenData.ExpiresAt > now + 60)
            return tokenData.AccessToken;

        var refreshResult = await stravaApiClient.RefreshTokenAsync(tokenData.RefreshToken);
        if (!refreshResult.Success)
            throw new InvalidOperationException($"Failed to refresh Strava token: {refreshResult.Error}");

        tokenData.AccessToken = refreshResult.AccessToken!;
        tokenData.RefreshToken = refreshResult.RefreshToken!;
        tokenData.ExpiresAt = refreshResult.ExpiresAt;
        await stravaTokenService.UpdateTokenDataAsync(participantId, tokenData);

        return tokenData.AccessToken;
    }

    private static string GenerateRandomPassword()
    {
        const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lower = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";
        const string all = upper + lower + digits;

        var random = System.Security.Cryptography.RandomNumberGenerator.Create();
        var bytes = new byte[16];
        random.GetBytes(bytes);

        var chars = new char[16];
        chars[0] = upper[bytes[0] % upper.Length];
        chars[1] = lower[bytes[1] % lower.Length];
        chars[2] = digits[bytes[2] % digits.Length];

        for (int i = 3; i < 16; i++)
            chars[i] = all[bytes[i] % all.Length];

        return new string(chars.OrderBy(_ => Guid.NewGuid()).ToArray());
    }

    private static string? GenerateSuggestedEmail(string firstName, string lastName, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)
            || string.IsNullOrWhiteSpace(firstName)
            || string.IsNullOrWhiteSpace(lastName))
            return null;

        return pattern
            .Replace("{first}", firstName.Trim().ToLowerInvariant())
            .Replace("{last}", lastName.Trim().ToLowerInvariant());
    }

    private async Task<Domain.Entities.Participant> GetParticipantForCurrentUserAsync(Guid eventId)
    {
        var userId = GetUserId();
        return await dbContext.Participants
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.EventId == eventId && p.UserId == userId)
            ?? throw new KeyNotFoundException("You are not registered for this event.");
    }

    private string GetUserId() =>
        User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? throw new UnauthorizedAccessException("User identity not found.");
}
