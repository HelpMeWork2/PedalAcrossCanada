using System.Net.Http.Headers;
using System.Net.Http.Json;
using PedalAcrossCanada.Auth;
using PedalAcrossCanada.Shared.DTOs.Auth;

namespace PedalAcrossCanada.Services;

public class AuthHttpService(
    HttpClient httpClient,
    TokenService tokenService,
    JwtAuthStateProvider authStateProvider)
{
    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var response = await httpClient.PostAsJsonAsync("api/auth/login", request);
        response.EnsureSuccessStatusCode();

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>()
            ?? throw new InvalidOperationException("Invalid login response.");

        await StoreTokensAndNotifyAsync(authResponse);
        return authResponse;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var response = await httpClient.PostAsJsonAsync("api/auth/register", request);
        response.EnsureSuccessStatusCode();

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>()
            ?? throw new InvalidOperationException("Invalid register response.");

        await StoreTokensAndNotifyAsync(authResponse);
        return authResponse;
    }

    public async Task LogoutAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout");
            if (!string.IsNullOrEmpty(tokenService.AccessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenService.AccessToken);
            }
            await httpClient.SendAsync(request);
        }
        catch
        {
            // Best-effort server-side logout
        }

        await tokenService.ClearAllTokensAsync();
        authStateProvider.NotifyUserLoggedOut();
    }

    public async Task<bool> TryRefreshAsync()
    {
        var refreshToken = await tokenService.GetRefreshTokenAsync();
        if (string.IsNullOrEmpty(refreshToken))
            return false;

        try
        {
            var response = await httpClient.PostAsJsonAsync("api/auth/refresh",
                new RefreshTokenRequest { RefreshToken = refreshToken });

            if (!response.IsSuccessStatusCode)
                return false;

            var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (authResponse is null)
                return false;

            await StoreTokensAndNotifyAsync(authResponse);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task StoreTokensAndNotifyAsync(AuthResponse authResponse)
    {
        tokenService.SetAccessToken(authResponse.AccessToken);
        await tokenService.SetRefreshTokenAsync(authResponse.RefreshToken);
        authStateProvider.NotifyAuthenticationStateChanged(authResponse.AccessToken);
    }
}
