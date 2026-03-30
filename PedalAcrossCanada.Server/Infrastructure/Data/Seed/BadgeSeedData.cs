using PedalAcrossCanada.Server.Domain.Entities;

namespace PedalAcrossCanada.Server.Infrastructure.Data.Seed;

public static class BadgeSeedData
{
    public static readonly Guid FirstRideId = new("a1b2c3d4-0001-0000-0000-000000000001");
    public static readonly Guid FiftyKmId = new("a1b2c3d4-0002-0000-0000-000000000002");
    public static readonly Guid CenturyId = new("a1b2c3d4-0003-0000-0000-000000000003");
    public static readonly Guid QuarterCrusherId = new("a1b2c3d4-0004-0000-0000-000000000004");
    public static readonly Guid FiveHundredId = new("a1b2c3d4-0005-0000-0000-000000000005");

    public static Badge[] GetDefaultBadges() =>
    [
        new Badge
        {
            Id = FirstRideId,
            Name = "First Ride",
            Description = "Completed your very first ride!",
            ThresholdKm = 0.01m,
            IsDefault = true,
            IsActive = true,
            SortOrder = 1
        },
        new Badge
        {
            Id = FiftyKmId,
            Name = "50 km Club",
            Description = "Reached 50 cumulative kilometres.",
            ThresholdKm = 50m,
            IsDefault = true,
            IsActive = true,
            SortOrder = 2
        },
        new Badge
        {
            Id = CenturyId,
            Name = "Century Rider",
            Description = "Reached 100 cumulative kilometres.",
            ThresholdKm = 100m,
            IsDefault = true,
            IsActive = true,
            SortOrder = 3
        },
        new Badge
        {
            Id = QuarterCrusherId,
            Name = "Quarter Crusher",
            Description = "Reached 250 cumulative kilometres.",
            ThresholdKm = 250m,
            IsDefault = true,
            IsActive = true,
            SortOrder = 4
        },
        new Badge
        {
            Id = FiveHundredId,
            Name = "500 km Legend",
            Description = "Reached 500 cumulative kilometres. Legendary!",
            ThresholdKm = 500m,
            IsDefault = true,
            IsActive = true,
            SortOrder = 5
        }
    ];
}
