using System.ComponentModel.DataAnnotations;

namespace PedalAcrossCanada.Shared.DTOs.Activities;

public record InvalidateActivityRequest
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; init; } = string.Empty;
}
