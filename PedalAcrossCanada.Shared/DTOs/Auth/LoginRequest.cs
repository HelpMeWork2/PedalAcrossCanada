using System.ComponentModel.DataAnnotations;

namespace PedalAcrossCanada.Shared.DTOs.Auth;

public record LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}
