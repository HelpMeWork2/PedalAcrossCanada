using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace PedalAcrossCanada.Auth;

/// <summary>
/// Stub AuthenticationStateProvider. Full JWT implementation added in Phase 3.
/// </summary>
public class JwtAuthStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState Unauthenticated =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private AuthenticationState _current = Unauthenticated;

    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(_current);

    public void NotifyAuthenticationStateChanged(AuthenticationState state)
    {
        _current = state;
        NotifyAuthenticationStateChanged(Task.FromResult(_current));
    }

    public void NotifyUserLoggedOut()
    {
        _current = Unauthenticated;
        NotifyAuthenticationStateChanged(Task.FromResult(_current));
    }
}
