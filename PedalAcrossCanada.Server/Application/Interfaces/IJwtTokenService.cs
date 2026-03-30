using PedalAcrossCanada.Server.Domain.Entities;

namespace PedalAcrossCanada.Server.Application.Interfaces;

public interface IJwtTokenService
{
    string GenerateAccessToken(ApplicationUser user, IList<string> roles);
    string GenerateRefreshToken();
}
