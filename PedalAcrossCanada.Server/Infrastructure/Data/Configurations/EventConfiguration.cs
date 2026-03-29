using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PedalAcrossCanada.Server.Domain.Entities;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Infrastructure.Data.Configurations;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.BannerMessage).HasMaxLength(1000);

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.ManualEntryMode)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(ManualEntryMode.AllowedWithApproval);

        builder.Property(e => e.StravaEnabled).HasDefaultValue(false);
        builder.Property(e => e.MaxSingleRideKm).HasDefaultValue(300m);
        builder.Property(e => e.LeaderboardPublic).HasDefaultValue(true);
        builder.Property(e => e.ShowTeamAverage).HasDefaultValue(true);

        builder.HasMany(e => e.Milestones)
            .WithOne(m => m.Event)
            .HasForeignKey(m => m.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Teams)
            .WithOne(t => t.Event)
            .HasForeignKey(t => t.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Participants)
            .WithOne(p => p.Event)
            .HasForeignKey(p => p.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Activities)
            .WithOne(a => a.Event)
            .HasForeignKey(a => a.EventId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
