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

    private sealed class StravaApiActivity
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public float Distance { get; set; }
        public DateTime StartDateLocal { get; set; }
    }

    private sealed class StravaTokenRefreshApiResponse
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public long ExpiresAt { get; set; }
    }
}
