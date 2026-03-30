using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Events;

namespace PedalAcrossCanada.Server.Application.Interfaces;

public interface IEventService
{
    Task<PagedResult<EventDto>> GetAllAsync(int page, int pageSize);
    Task<EventDto> GetByIdAsync(Guid eventId);
    Task<EventDto> CreateAsync(CreateEventRequest request, string actor);
    Task<EventDto> UpdateAsync(Guid eventId, UpdateEventRequest request, string actor);
    Task<EventDto> ActivateAsync(Guid eventId, string actor);
    Task<EventDto> CloseAsync(Guid eventId, string actor);
    Task<EventDto> ArchiveAsync(Guid eventId, string actor);
    Task<EventDto> RevertToDraftAsync(Guid eventId, string actor);
    Task<EventDto?> GetActiveEventAsync();
}
