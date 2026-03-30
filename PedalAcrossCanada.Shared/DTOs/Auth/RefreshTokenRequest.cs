using System.ComponentModel.DataAnnotations;

namespace PedalAcrossCanada.Shared.DTOs.Auth;

public record RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}
