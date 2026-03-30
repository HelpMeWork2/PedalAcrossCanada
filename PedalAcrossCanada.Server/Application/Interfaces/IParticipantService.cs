using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Participants;

namespace PedalAcrossCanada.Server.Application.Interfaces;

public interface IParticipantService
{
    Task<PagedResult<ParticipantDto>> GetAllByEventAsync(Guid eventId, int page, int pageSize);
    Task<ParticipantDto> GetByIdAsync(Guid eventId, Guid participantId);
    Task<ParticipantDto> GetByUserIdAsync(Guid eventId, string userId);
    Task<ParticipantDto> CreateAsync(Guid eventId, CreateParticipantRequest request, string userId, string actor);
    Task<ParticipantDto> UpdateAsync(Guid eventId, Guid participantId, UpdateParticipantRequest request, string actor);
    Task<ParticipantDto> DeactivateAsync(Guid eventId, Guid participantId, string actor);
    Task<ParticipantDto> ReactivateAsync(Guid eventId, Guid participantId, string actor);
    Task<ParticipantDto> ChangeTeamAsync(Guid eventId, Guid participantId, Guid teamId, string actor);
}
