using PedalAcrossCanada.Shared.DTOs.Badges;

namespace PedalAcrossCanada.Services;

public class BadgeHttpService(ApiClient apiClient)
{
    public async Task<IReadOnlyList<BadgeDto>?> GetAllForEventAsync(Guid eventId)
    {
        return await apiClient.GetAsync<List<BadgeDto>>($"api/events/{eventId}/badges");
    }

    public async Task<IReadOnlyList<BadgeAwardDto>?> GetMyAwardsAsync(Guid eventId)
    {
        return await apiClient.GetAsync<List<BadgeAwardDto>>($"api/events/{eventId}/badges/my-awards");
    }

    public async Task<IReadOnlyList<BadgeAwardDto>?> GetParticipantAwardsAsync(Guid eventId, Guid participantId)
    {
        return await apiClient.GetAsync<List<BadgeAwardDto>>(
            $"api/events/{eventId}/badges/participants/{participantId}/awards");
    }

    public async Task<HttpResponseMessage> GrantBadgeAsync(Guid eventId, Guid badgeId, GrantBadgeRequest request)
    {
        return await apiClient.PostAsync($"api/events/{eventId}/badges/{badgeId}/grant", request);
    }
}
