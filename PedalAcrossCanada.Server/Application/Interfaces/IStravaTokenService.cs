using PedalAcrossCanada.Shared.DTOs.Strava;

namespace PedalAcrossCanada.Server.Application.Interfaces;

public interface IStravaTokenService
{
    string BuildAuthorizationUrl(Guid participantId);
    Task<StravaStatusDto> ExchangeCodeAsync(string code, Guid participantId, string actor);
    Task DisconnectAsync(Guid participantId, string actor);
    Task<StravaStatusDto> GetStatusAsync(Guid participantId);
    Task<StravaTokenData?> GetTokenDataAsync(Guid participantId);
    Task UpdateTokenDataAsync(Guid participantId, StravaTokenData tokenData);
}

public class StravaTokenData
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public long ExpiresAt { get; set; }
    public string AthleteId { get; set; } = string.Empty;
}
