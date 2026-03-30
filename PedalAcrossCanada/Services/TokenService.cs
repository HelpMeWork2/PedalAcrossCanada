using Blazored.LocalStorage;

namespace PedalAcrossCanada.Services;

public class TokenService(ILocalStorageService localStorage)
{
    private const string RefreshTokenKey = "refreshToken";
    private string? _accessToken;

    public string? AccessToken => _accessToken;

    public void SetAccessToken(string token)
    {
        _accessToken = token;
    }

    public void ClearAccessToken()
    {
        _accessToken = null;
    }

    public async Task SetRefreshTokenAsync(string token)
    {
        await localStorage.SetItemAsStringAsync(RefreshTokenKey, token);
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        return await localStorage.GetItemAsStringAsync(RefreshTokenKey);
    }

    public async Task ClearRefreshTokenAsync()
    {
        await localStorage.RemoveItemAsync(RefreshTokenKey);
    }

    public async Task ClearAllTokensAsync()
    {
        ClearAccessToken();
        await ClearRefreshTokenAsync();
    }
}
