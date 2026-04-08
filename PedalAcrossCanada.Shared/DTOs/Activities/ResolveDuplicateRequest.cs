using System.ComponentModel.DataAnnotations;

namespace PedalAcrossCanada.Shared.DTOs.Activities;

public record ResolveDuplicateRequest
{
    [Required]
    public DuplicateResolution Resolution { get; init; }
}

public enum DuplicateResolution
{
    KeepBoth,
    KeepFirst,
    KeepSecond
}
