using PedalAcrossCanada.Shared.Common;
using PedalAcrossCanada.Shared.DTOs.Activities;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Application.Interfaces;

public interface IActivityService
{
    Task<PagedResult<ActivityDto>> GetAllAsync(
        Guid eventId,
        int page,
        int pageSize,
        ActivityStatus? status = null,
        ActivitySource? source = null,
        Guid? participantId = null,
        Guid? teamId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        bool? duplicateFlagged = null);

    Task<PagedResult<ActivityDto>> GetByParticipantAsync(
        Guid eventId,
        Guid participantId,
        int page,
        int pageSize);

    Task<ActivityDto> GetByIdAsync(Guid eventId, Guid activityId);

    Task<CreateActivityResponse> CreateAsync(
        Guid eventId,
        Guid participantId,
        CreateActivityRequest request,
        string actor);

    Task<ActivityDto> UpdateAsync(
        Guid eventId,
        Guid activityId,
        UpdateActivityRequest request,
        string actor);

    Task DeleteAsync(Guid eventId, Guid activityId, string actor);

    Task<ActivityDto> ApproveAsync(Guid eventId, Guid activityId, string actor);

    Task<ActivityDto> RejectAsync(
        Guid eventId,
        Guid activityId,
        RejectActivityRequest request,
        string actor);

    Task<ActivityDto> InvalidateAsync(
        Guid eventId,
        Guid activityId,
        InvalidateActivityRequest request,
        string actor);

    Task<ActivityDto> LockAsync(Guid eventId, Guid activityId, string actor);
}
