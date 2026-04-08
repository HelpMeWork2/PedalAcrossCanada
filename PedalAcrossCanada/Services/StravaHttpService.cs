using System.Net.Http.Json;
using PedalAcrossCanada.Shared.DTOs.Strava;

namespace PedalAcrossCanada.Services;

public class StravaHttpService(ApiClient apiClient)
{
    public async Task<StravaAuthUrlDto?> GetAuthUrlAsync(Guid eventId)
    {
        return await apiClient.GetAsync<StravaAuthUrlDto>($"api/strava/auth-url?eventId={eventId}");
    }

    public async Task<StravaStatusDto?> CallbackAsync(string code, Guid participantId)
    {
        var response = await apiClient.PostAsync($"api/strava/callback?code={Uri.EscapeDataString(code)}&state={participantId}", new { });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StravaStatusDto>();
    }

    public async Task DisconnectAsync(Guid eventId)
    {
        var response = await apiClient.PostAsync($"api/strava/disconnect?eventId={eventId}", new { });
        response.EnsureSuccessStatusCode();
    }

    public async Task<StravaStatusDto?> GetStatusAsync(Guid eventId)
    {
        return await apiClient.GetAsync<StravaStatusDto>($"api/strava/status?eventId={eventId}");
    }

    public async Task<StravaSyncResultDto?> ManualSyncAsync(Guid eventId)
    {
        var response = await apiClient.PostAsync($"api/strava/sync?eventId={eventId}", new { });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StravaSyncResultDto>();
    }

    public async Task<BulkStravaSyncResultDto?> SyncAllAsync(Guid eventId)
    {
        var response = await apiClient.PostAsync($"api/strava/sync-all?eventId={eventId}", new { });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BulkStravaSyncResultDto>();
    }

    public async Task<ClubActivitySyncResultDto?> SyncClubActivitiesAsync(Guid eventId)
    {
        var response = await apiClient.PostAsync($"api/strava/sync-club-activities?eventId={eventId}", new { });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ClubActivitySyncResultDto>();
    }

    public async Task<List<StravaClubMemberDto>?> GetClubMembersAsync(Guid eventId)
    {
        return await apiClient.GetAsync<List<StravaClubMemberDto>>($"api/strava/club-members?eventId={eventId}");
    }

    public async Task<ImportClubMembersResultDto?> ImportClubMembersAsync(Guid eventId, ImportClubMembersRequest request)
    {
        var response = await apiClient.PostAsync($"api/strava/import-club-members?eventId={eventId}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ImportClubMembersResultDto>();
    }
}
