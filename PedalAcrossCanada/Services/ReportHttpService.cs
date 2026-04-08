namespace PedalAcrossCanada.Services;

public class ReportHttpService(ApiClient apiClient)
{
    public Task<byte[]> GetParticipantsReportAsync(
        Guid eventId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        Guid? teamId = null)
    {
        var query = BuildQueryString(startDate, endDate, teamId: teamId);
        return apiClient.GetBytesAsync($"api/events/{eventId}/reports/participants{query}");
    }

    public Task<byte[]> GetActivitiesReportAsync(
        Guid eventId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        Guid? teamId = null,
        Guid? participantId = null)
    {
        var query = BuildQueryString(startDate, endDate, teamId: teamId, participantId: participantId);
        return apiClient.GetBytesAsync($"api/events/{eventId}/reports/activities{query}");
    }

    public Task<byte[]> GetTeamTotalsReportAsync(
        Guid eventId,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var query = BuildQueryString(startDate, endDate);
        return apiClient.GetBytesAsync($"api/events/{eventId}/reports/teams{query}");
    }

    public Task<byte[]> GetMilestonesReportAsync(Guid eventId)
    {
        return apiClient.GetBytesAsync($"api/events/{eventId}/reports/milestones");
    }

    public Task<byte[]> GetBadgeAwardsReportAsync(
        Guid eventId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        Guid? teamId = null,
        Guid? participantId = null)
    {
        var query = BuildQueryString(startDate, endDate, teamId: teamId, participantId: participantId);
        return apiClient.GetBytesAsync($"api/events/{eventId}/reports/badges{query}");
    }

    public Task<byte[]> GetExecutiveSummaryReportAsync(Guid eventId)
    {
        return apiClient.GetBytesAsync($"api/events/{eventId}/reports/executive-summary");
    }

    private static string BuildQueryString(
        DateTime? startDate = null,
        DateTime? endDate = null,
        Guid? teamId = null,
        Guid? participantId = null)
    {
        var parts = new List<string>();

        if (startDate.HasValue)
            parts.Add($"startDate={startDate.Value:O}");

        if (endDate.HasValue)
            parts.Add($"endDate={endDate.Value:O}");

        if (teamId.HasValue)
            parts.Add($"teamId={teamId.Value}");

        if (participantId.HasValue)
            parts.Add($"participantId={participantId.Value}");

        return parts.Count > 0 ? "?" + string.Join("&", parts) : string.Empty;
    }
}
