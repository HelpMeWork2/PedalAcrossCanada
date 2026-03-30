using PedalAcrossCanada.Shared.DTOs.Auth;

namespace PedalAcrossCanada.Server.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> RefreshAsync(RefreshTokenRequest request);
    Task LogoutAsync(string userId);
    Task<UserInfoDto> GetCurrentUserAsync(string userId);
}
