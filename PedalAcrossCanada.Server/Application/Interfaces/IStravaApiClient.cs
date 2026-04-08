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

    Task<IReadOnlyList<StravaClubMember>> GetClubMembersAsync(
        string accessToken,
        string clubId,
        int page = 1,
        int perPage = 200);

    Task<IReadOnlyList<StravaClubActivity>> GetClubActivitiesAsync(
        string accessToken,
        string clubId,
        int page = 1,
        int perPage = 200);
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

public class StravaClubMember
{
    public long AthleteId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? ProfilePictureUrl { get; set; }
    public bool IsOwner { get; set; }
    public bool IsAdmin { get; set; }
}

public class StravaClubActivity
{
    public string AthleteFirstName { get; set; } = string.Empty;
    public string AthleteLastName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public float Distance { get; set; }
    public int MovingTime { get; set; }
    public int ElapsedTime { get; set; }
    public float TotalElevationGain { get; set; }
    public string Type { get; set; } = string.Empty;
    public string SportType { get; set; } = string.Empty;
    public DateTime? StartDateLocal { get; set; }
}
