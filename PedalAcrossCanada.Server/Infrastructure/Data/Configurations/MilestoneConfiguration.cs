using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PedalAcrossCanada.Server.Domain.Entities;

namespace PedalAcrossCanada.Server.Infrastructure.Data.Configurations;

public class MilestoneConfiguration : IEntityTypeConfiguration<Milestone>
{
    public void Configure(EntityTypeBuilder<Milestone> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedOnAdd();

        builder.Property(m => m.StopName).IsRequired().HasMaxLength(200);
        builder.Property(m => m.Description).HasMaxLength(1000);
        builder.Property(m => m.RewardText).HasMaxLength(500);
        builder.Property(m => m.AnnouncedBy).HasMaxLength(450);

        builder.Property(m => m.AnnouncementStatus)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.HasIndex(m => new { m.EventId, m.OrderIndex }).IsUnique();
    }
}
