using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Server.Configuration;
using PedalAcrossCanada.Server.Domain.Entities;
using PedalAcrossCanada.Server.Infrastructure.Data;
using PedalAcrossCanada.Shared.DTOs.Strava;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Application.Services;

public class StravaTokenService(
    AppDbContext dbContext,
    ITokenEncryptionService tokenEncryptionService,
    IAuditService auditService,
    IOptions<StravaSettings> stravaSettings) : IStravaTokenService
{
    private readonly StravaSettings _settings = stravaSettings.Value;

    public string BuildAuthorizationUrl(Guid participantId)
    {
        var redirectUri = Uri.EscapeDataString(_settings.RedirectUri);
        return $"https://www.strava.com/oauth/authorize" +
               $"?client_id={_settings.ClientId}" +
               $"&redirect_uri={redirectUri}" +
               $"&response_type=code" +
               $"&scope=read,activity:read" +
               $"&state={participantId}";
    }

    public async Task<StravaStatusDto> ExchangeCodeAsync(string code, Guid participantId, string actor)
    {
        var participant = await dbContext.Participants
            .FirstOrDefaultAsync(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant '{participantId}' not found.");

        using var httpClient = new HttpClient();
        var response = await httpClient.PostAsync("https://www.strava.com/oauth/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["client_id"] = _settings.ClientId,
                ["client_secret"] = _settings.ClientSecret,
                ["code"] = code,
                ["grant_type"] = "authorization_code"
            }));

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<StravaOAuthResponse>(json, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("Failed to parse Strava token response.");

        var tokenData = new StravaTokenData
        {
            AccessToken = tokenResponse.AccessToken ?? string.Empty,
            RefreshToken = tokenResponse.RefreshToken ?? string.Empty,
            ExpiresAt = tokenResponse.ExpiresAt,
            AthleteId = tokenResponse.Athlete?.Id.ToString() ?? string.Empty
        };

        var encryptedData = tokenEncryptionService.Encrypt(JsonSerializer.Serialize(tokenData));

        var existing = await dbContext.ExternalConnections
            .FirstOrDefaultAsync(ec => ec.ParticipantId == participantId && ec.Provider == "Strava");

        if (existing is not null)
        {
            existing.ExternalAthleteId = tokenData.AthleteId;
            existing.EncryptedTokenData = encryptedData;
            existing.ConnectionStatus = ConnectionStatus.Connected;
            existing.ConnectedAt = DateTime.UtcNow;
            existing.DisconnectedAt = null;
        }
        else
        {
            var connection = new ExternalConnection
            {
                ParticipantId = participantId,
                Provider = "Strava",
                ExternalAthleteId = tokenData.AthleteId,
                EncryptedTokenData = encryptedData,
                ConnectionStatus = ConnectionStatus.Connected,
                ConnectedAt = DateTime.UtcNow
            };
            dbContext.ExternalConnections.Add(connection);
        }

        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor,
            "StravaConnected",
            "ExternalConnection",
            participantId.ToString(),
            participant.EventId);

        return await GetStatusAsync(participantId);
    }

    public async Task DisconnectAsync(Guid participantId, string actor)
    {
        var connection = await dbContext.ExternalConnections
            .FirstOrDefaultAsync(ec => ec.ParticipantId == participantId && ec.Provider == "Strava")
            ?? throw new KeyNotFoundException("Strava connection not found.");

        var participant = await dbContext.Participants
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant '{participantId}' not found.");

        connection.ConnectionStatus = ConnectionStatus.Disconnected;
        connection.DisconnectedAt = DateTime.UtcNow;
        connection.EncryptedTokenData = string.Empty;

        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(
            actor,
            "StravaDisconnected",
            "ExternalConnection",
            participantId.ToString(),
            participant.EventId);
    }

    public async Task<StravaStatusDto> GetStatusAsync(Guid participantId)
    {
        var connection = await dbContext.ExternalConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(ec => ec.ParticipantId == participantId && ec.Provider == "Strava");

        if (connection is null)
        {
            return new StravaStatusDto { IsConnected = false };
        }

        return new StravaStatusDto
        {
            IsConnected = connection.ConnectionStatus == ConnectionStatus.Connected,
            ConnectionStatus = connection.ConnectionStatus,
            ExternalAthleteId = connection.ExternalAthleteId,
            LastSyncAt = connection.LastSyncAt,
            ConnectedAt = connection.ConnectedAt
        };
    }

    public async Task<StravaTokenData?> GetTokenDataAsync(Guid participantId)
    {
        var connection = await dbContext.ExternalConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(ec => ec.ParticipantId == participantId && ec.Provider == "Strava");

        if (connection is null || connection.ConnectionStatus != ConnectionStatus.Connected
            || string.IsNullOrEmpty(connection.EncryptedTokenData))
        {
            return null;
        }

        var decrypted = tokenEncryptionService.Decrypt(connection.EncryptedTokenData);
        return JsonSerializer.Deserialize<StravaTokenData>(decrypted);
    }

    public async Task UpdateTokenDataAsync(Guid participantId, StravaTokenData tokenData)
    {
        var connection = await dbContext.ExternalConnections
            .FirstOrDefaultAsync(ec => ec.ParticipantId == participantId && ec.Provider == "Strava")
            ?? throw new KeyNotFoundException("Strava connection not found.");

        var encryptedData = tokenEncryptionService.Encrypt(JsonSerializer.Serialize(tokenData));
        connection.EncryptedTokenData = encryptedData;
        await dbContext.SaveChangesAsync();
    }

    private sealed class StravaOAuthResponse
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public long ExpiresAt { get; set; }
        public StravaAthlete? Athlete { get; set; }
    }

    private sealed class StravaAthlete
    {
        public long Id { get; set; }
    }
}
