using PedalAcrossCanada.Shared.DTOs.Milestones;

namespace PedalAcrossCanada.Server.Application.Interfaces;

public interface IMilestoneService
{
    Task<IReadOnlyList<MilestoneDto>> GetAllByEventAsync(Guid eventId);
    Task<MilestoneDto> GetByIdAsync(Guid eventId, Guid milestoneId);
    Task<MilestoneDto> CreateAsync(Guid eventId, CreateMilestoneRequest request, string actor);
    Task<MilestoneDto> UpdateAsync(Guid eventId, Guid milestoneId, UpdateMilestoneRequest request, string actor);
    Task DeleteAsync(Guid eventId, Guid milestoneId, string actor);
    Task<MilestoneDto> AnnounceAsync(Guid eventId, Guid milestoneId, string actor);
}
