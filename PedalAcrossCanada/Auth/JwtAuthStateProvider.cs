using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using System.Text.Json;

namespace PedalAcrossCanada.Auth;

/// <summary>
/// Parses JWT claims from the access token to provide authentication state to Blazor.
/// </summary>
public class JwtAuthStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState Unauthenticated =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private AuthenticationState _current = Unauthenticated;

    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(_current);

    public void NotifyAuthenticationStateChanged(string accessToken)
    {
        var claims = ParseClaimsFromJwt(accessToken);
        var identity = new ClaimsIdentity(claims, "jwt", "sub", "role");
        _current = new AuthenticationState(new ClaimsPrincipal(identity));
        NotifyAuthenticationStateChanged(Task.FromResult(_current));
    }

    public void NotifyUserLoggedOut()
    {
        _current = Unauthenticated;
        NotifyAuthenticationStateChanged(Task.FromResult(_current));
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var jsonBytes = ParseBase64WithoutPadding(payload);
        var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonBytes)!;

        var claims = new List<Claim>();

        foreach (var kvp in keyValuePairs)
        {
            if (kvp.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in kvp.Value.EnumerateArray())
                {
                    claims.Add(new Claim(kvp.Key, element.GetString()!));
                }
            }
            else
            {
                claims.Add(new Claim(kvp.Key, kvp.Value.ToString()));
            }
        }

        return claims;
    }

    private static byte[] ParseBase64WithoutPadding(string base64)
    {
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        return Convert.FromBase64String(base64);
    }
}
