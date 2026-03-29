namespace PedalAcrossCanada.Server.Domain.Entities;

public class Badge
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? ThresholdKm { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }

    public ICollection<BadgeAward> BadgeAwards { get; set; } = [];
}
