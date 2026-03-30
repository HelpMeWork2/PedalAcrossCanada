using PedalAcrossCanada.Server.Application.Interfaces;

namespace PedalAcrossCanada.Server.Tests.Fakes;

/// <summary>
/// Configurable fake for <see cref="IStravaApiClient"/> that returns canned responses
/// without making real HTTP calls to Strava.
/// </summary>
public sealed class FakeStravaApiClient : IStravaApiClient
{
    /// <summary>Activities returned by <see cref="GetActivitiesAsync"/>. Keyed by page number.</summary>
    public Dictionary<int, List<StravaActivityData>> ActivitiesByPage { get; } = [];

    /// <summary>Result returned by <see cref="RefreshTokenAsync"/>.</summary>
    public StravaTokenRefreshResult? RefreshResult { get; set; }

    /// <summary>If set, <see cref="GetActivitiesAsync"/> throws this exception.</summary>
    public Exception? GetActivitiesException { get; set; }

    /// <summary>Records every call to <see cref="GetActivitiesAsync"/> for assertion.</summary>
    public List<GetActivitiesCall> GetActivitiesCalls { get; } = [];

    /// <summary>Records every call to <see cref="RefreshTokenAsync"/> for assertion.</summary>
    public List<string> RefreshTokenCalls { get; } = [];

    public Task<IReadOnlyList<StravaActivityData>> GetActivitiesAsync(
        string accessToken,
        DateTime after,
        DateTime before,
        int page = 1,
        int perPage = 100)
    {
        GetActivitiesCalls.Add(new GetActivitiesCall(accessToken, after, before, page, perPage));

        if (GetActivitiesException is not null)
            throw GetActivitiesException;

        if (ActivitiesByPage.TryGetValue(page, out var activities))
            return Task.FromResult<IReadOnlyList<StravaActivityData>>(activities);

        return Task.FromResult<IReadOnlyList<StravaActivityData>>([]);
    }

    public Task<StravaTokenRefreshResult> RefreshTokenAsync(string refreshToken)
    {
        RefreshTokenCalls.Add(refreshToken);

        return Task.FromResult(RefreshResult ?? new StravaTokenRefreshResult
        {
            Success = false,
            Error = "No refresh result configured."
        });
    }

    public record GetActivitiesCall(
        string AccessToken,
        DateTime After,
        DateTime Before,
        int Page,
        int PerPage);
}
