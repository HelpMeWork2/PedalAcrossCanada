using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PedalAcrossCanada.Server.Application.Interfaces;
using PedalAcrossCanada.Server.Configuration;

namespace PedalAcrossCanada.Server.Application.Services;

public class StravaApiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<StravaSettings> stravaSettings) : IStravaApiClient
{
    private readonly StravaSettings _settings = stravaSettings.Value;

    public async Task<IReadOnlyList<StravaActivityData>> GetActivitiesAsync(
        string accessToken,
        DateTime after,
        DateTime before,
        int page = 1,
        int perPage = 100)
    {
        var client = httpClientFactory.CreateClient("Strava");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var afterEpoch = new DateTimeOffset(after, TimeSpan.Zero).ToUnixTimeSeconds();
        var beforeEpoch = new DateTimeOffset(before, TimeSpan.Zero).ToUnixTimeSeconds();

        var url = $"https://www.strava.com/api/v3/athlete/activities" +
                  $"?after={afterEpoch}&before={beforeEpoch}&page={page}&per_page={perPage}";

        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var activities = JsonSerializer.Deserialize<List<StravaApiActivity>>(json, JsonSerializerOptions.Web) ?? [];

        return activities.Select(a => new StravaActivityData
        {
            Id = a.Id,
            Name = a.Name ?? string.Empty,
            Type = a.Type ?? string.Empty,
            Distance = a.Distance,
            StartDateLocal = a.StartDateLocal
        }).ToList();
    }

    public async Task<StravaTokenRefreshResult> RefreshTokenAsync(string refreshToken)
    {
        var client = httpClientFactory.CreateClient("Strava");
        var response = await client.PostAsync("https://www.strava.com/oauth/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _settings.ClientId,
                ["client_secret"] = _settings.ClientSecret,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken
            }));

        if (!response.IsSuccessStatusCode)
        {
            return new StravaTokenRefreshResult
            {
                Success = false,
                Error = $"Token refresh failed with status {(int)response.StatusCode}"
            };
        }

        var json = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<StravaTokenRefreshApiResponse>(json, JsonSerializerOptions.Web);

        return new StravaTokenRefreshResult
        {
            Success = true,
            AccessToken = tokenResponse?.AccessToken,
            RefreshToken = tokenResponse?.RefreshToken,
            ExpiresAt = tokenResponse?.ExpiresAt ?? 0
        };
    }

    public async Task<IReadOnlyList<StravaClubMember>> GetClubMembersAsync(
        string accessToken,
        string clubId,
        int page = 1,
        int perPage = 200)
    {
        var client = httpClientFactory.CreateClient("Strava");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var allMembers = new List<StravaClubMember>();
        var currentPage = page;

        while (true)
        {
            var url = $"https://www.strava.com/api/v3/clubs/{clubId}/members" +
                      $"?page={currentPage}&per_page={perPage}";

            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var members = JsonSerializer.Deserialize<List<StravaClubMemberApiResponse>>(json, JsonSerializerOptions.Web) ?? [];

            if (members.Count == 0)
                break;

            allMembers.AddRange(members.Select(m => new StravaClubMember
            {
                AthleteId = m.Id,
                FirstName = m.FirstName ?? string.Empty,
                LastName = m.LastName ?? string.Empty,
                ProfilePictureUrl = m.Profile,
                IsOwner = m.Owner,
                IsAdmin = m.Admin
            }));

            if (members.Count < perPage)
                break;

            currentPage++;
        }

        return allMembers;
    }

    public async Task<IReadOnlyList<StravaClubActivity>> GetClubActivitiesAsync(
        string accessToken,
        string clubId,
        int page = 1,
        int perPage = 200)
    {
        var client = httpClientFactory.CreateClient("Strava");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var allActivities = new List<StravaClubActivity>();
        var currentPage = page;

        while (true)
        {
            var url = $"https://www.strava.com/api/v3/clubs/{clubId}/activities" +
                      $"?page={currentPage}&per_page={perPage}";

            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var activities = JsonSerializer.Deserialize<List<StravaClubActivityApiResponse>>(json, JsonSerializerOptions.Web) ?? [];

            if (activities.Count == 0)
                break;

            allActivities.AddRange(activities.Select(a => new StravaClubActivity
            {
                AthleteFirstName = a.Athlete?.FirstName ?? string.Empty,
                AthleteLastName = a.Athlete?.LastName ?? string.Empty,
                Name = a.Name ?? string.Empty,
                Distance = a.Distance,
                MovingTime = a.MovingTime,
                ElapsedTime = a.ElapsedTime,
                TotalElevationGain = a.TotalElevationGain,
                Type = a.Type ?? string.Empty,
                SportType = a.SportType ?? string.Empty,
                StartDateLocal = a.StartDateLocal
            }));

            if (activities.Count < perPage)
                break;

            currentPage++;
        }

        return allActivities;
    }

    private sealed class StravaApiActivity
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public long Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string? Name { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("type")]
        public string? Type { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("distance")]
        public float Distance { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("start_date_local")]
        public DateTime StartDateLocal { get; set; }
    }

    private sealed class StravaTokenRefreshApiResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("expires_at")]
        public long ExpiresAt { get; set; }
    }

    private sealed class StravaClubMemberApiResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public long Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("firstname")]
        public string? FirstName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("lastname")]
        public string? LastName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("profile")]
        public string? Profile { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("owner")]
        public bool Owner { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("admin")]
        public bool Admin { get; set; }
    }

    private sealed class StravaClubActivityApiResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("athlete")]
        public StravaClubActivityAthleteResponse? Athlete { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string? Name { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("distance")]
        public float Distance { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("moving_time")]
        public int MovingTime { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("elapsed_time")]
        public int ElapsedTime { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("total_elevation_gain")]
        public float TotalElevationGain { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("type")]
        public string? Type { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("sport_type")]
        public string? SportType { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("start_date_local")]
        public DateTime? StartDateLocal { get; set; }
    }

    private sealed class StravaClubActivityAthleteResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("firstname")]
        public string? FirstName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("lastname")]
        public string? LastName { get; set; }
    }
}
