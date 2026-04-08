namespace PedalAcrossCanada.Server.Configuration;

public class StravaSettings
{
    public const string SectionName = "Strava";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string ClubId { get; set; } = string.Empty;

    /// <summary>
    /// Optional pattern for auto-suggesting work emails during club member import.
    /// Use {first} and {last} as placeholders, e.g. "{first}.{last}@company.com".
    /// </summary>
    public string? EmailDomainPattern { get; set; }
}
