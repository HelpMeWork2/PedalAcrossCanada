namespace PedalAcrossCanada.Shared.Common;

public class ValidationErrorResponse
{
    public Dictionary<string, string[]> Errors { get; init; } = [];
}
