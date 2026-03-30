using PedalAcrossCanada.Shared.DTOs.Teams;

namespace PedalAcrossCanada.Server.Application.Interfaces;

public interface ITeamService
{
    Task<IReadOnlyList<TeamDto>> GetAllByEventAsync(Guid eventId);
    Task<TeamDto> GetByIdAsync(Guid eventId, Guid teamId);
    Task<TeamDto> CreateAsync(Guid eventId, CreateTeamRequest request, string actor);
    Task<TeamDto> UpdateAsync(Guid eventId, Guid teamId, UpdateTeamRequest request, string actor);
    Task DeleteAsync(Guid eventId, Guid teamId, string actor);
    Task<TeamDto> SetCaptainAsync(Guid eventId, Guid teamId, Guid participantId, string actor);
}
