namespace PedalAcrossCanada.Server.Application.Interfaces;

public interface IReportService
{
    Task<byte[]> GetParticipantsReportAsync(
        Guid eventId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        Guid? teamId = null);

    Task<byte[]> GetActivitiesReportAsync(
        Guid eventId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        Guid? teamId = null,
        Guid? participantId = null);

    Task<byte[]> GetTeamTotalsReportAsync(
        Guid eventId,
        DateTime? startDate = null,
        DateTime? endDate = null);

    Task<byte[]> GetMilestonesReportAsync(Guid eventId);

    Task<byte[]> GetBadgeAwardsReportAsync(
        Guid eventId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        Guid? teamId = null,
        Guid? participantId = null);

    Task<byte[]> GetExecutiveSummaryReportAsync(Guid eventId);
}
