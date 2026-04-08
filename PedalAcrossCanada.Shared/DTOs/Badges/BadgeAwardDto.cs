namespace PedalAcrossCanada.Shared.DTOs.Badges;

public class BadgeAwardDto
{
    public Guid Id { get; set; }
    public Guid BadgeId { get; set; }
    public string BadgeName { get; set; } = string.Empty;
    public string? BadgeDescription { get; set; }
    public decimal? ThresholdKm { get; set; }
    public DateTime AwardedAt { get; set; }
    public bool IsManual { get; set; }
    public string? AwardedBy { get; set; }
}
