using PedalAcrossCanada.Shared.DTOs.Dashboards;

namespace PedalAcrossCanada.Services;

public class DashboardHttpService(ApiClient apiClient)
{
    public async Task<EventDashboardDto?> GetEventDashboardAsync(Guid eventId)
    {
        return await apiClient.GetAsync<EventDashboardDto>(
            $"api/events/{eventId}/dashboards/event");
    }

    public async Task<ParticipantDashboardDto?> GetMyDashboardAsync(Guid eventId)
    {
        return await apiClient.GetAsync<ParticipantDashboardDto>(
            $"api/events/{eventId}/dashboards/me");
    }

    public async Task<AdminDashboardDto?> GetAdminDashboardAsync(Guid eventId)
    {
        return await apiClient.GetAsync<AdminDashboardDto>(
            $"api/events/{eventId}/dashboards/admin");
    }
}
