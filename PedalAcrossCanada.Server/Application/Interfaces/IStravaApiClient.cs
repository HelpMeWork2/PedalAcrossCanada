namespace PedalAcrossCanada.Server.Application.Interfaces;

public interface IStravaApiClient
{
    Task<IReadOnlyList<StravaActivityData>> GetActivitiesAsync(
        string accessToken,
        DateTime after,
        DateTime before,
        int page = 1,
        int perPage = 100);

    Task<StravaTokenRefreshResult> RefreshTokenAsync(string refreshToken);
}

public class StravaActivityData
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public float Distance { get; set; }
    public DateTime StartDateLocal { get; set; }
}

public class StravaTokenRefreshResult
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public long ExpiresAt { get; set; }
    public string? Error { get; set; }
}
