using PedalAcrossCanada.Shared.DTOs.Dashboards;

namespace PedalAcrossCanada.Server.Application.Interfaces;

public interface IDashboardService
{
    Task<EventDashboardDto> GetEventDashboardAsync(Guid eventId);
    Task<ParticipantDashboardDto> GetParticipantDashboardAsync(Guid eventId, Guid participantId);
    Task<AdminDashboardDto> GetAdminDashboardAsync(Guid eventId);
}
