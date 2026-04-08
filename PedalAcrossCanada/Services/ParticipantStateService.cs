using PedalAcrossCanada.Shared.DTOs.Participants;

namespace PedalAcrossCanada.Services;

/// <summary>
/// Client-side singleton that caches the authenticated user's own ParticipantDto
/// for the currently active event. Invalidated on logout or explicit refresh.
/// </summary>
public class ParticipantStateService(
    EventHttpService eventService,
    ParticipantHttpService participantService)
{
    private ParticipantDto? _participant;
    private bool _loaded;

    /// <summary>
    /// Returns the cached participant, loading it from the API on first access.
    /// Returns null if the user is not a registered participant in the active event.
    /// </summary>
    public async Task<ParticipantDto?> GetAsync()
    {
        if (_loaded)
            return _participant;

        await LoadAsync();
        return _participant;
    }

    /// <summary>
    /// Forces a fresh load from the API, bypassing the cache.
    /// </summary>
    public async Task RefreshAsync()
    {
        _loaded = false;
        await LoadAsync();
    }

    /// <summary>
    /// Clears the cached participant state. Call on logout.
    /// </summary>
    public void Clear()
    {
        _participant = null;
        _loaded = false;
    }

    private async Task LoadAsync()
    {
        try
        {
            var activeEvent = await eventService.GetActiveEventAsync();
            if (activeEvent is null)
            {
                _participant = null;
                _loaded = true;
                return;
            }

            _participant = await participantService.GetMeAsync(activeEvent.Id);
        }
        catch (HttpRequestException)
        {
            _participant = null;
        }
        finally
        {
            _loaded = true;
        }
    }
}
