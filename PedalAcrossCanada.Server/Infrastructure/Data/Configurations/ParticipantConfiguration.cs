using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PedalAcrossCanada.Server.Domain.Entities;
using PedalAcrossCanada.Shared.Enums;

namespace PedalAcrossCanada.Server.Infrastructure.Data.Configurations;

public class ParticipantConfiguration : IEntityTypeConfiguration<Participant>
{
    public void Configure(EntityTypeBuilder<Participant> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedOnAdd();

        builder.Property(p => p.UserId).IsRequired().HasMaxLength(450);
        builder.Property(p => p.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(p => p.LastName).IsRequired().HasMaxLength(100);
        builder.Property(p => p.WorkEmail).IsRequired().HasMaxLength(256);
        builder.Property(p => p.DisplayName).IsRequired().HasMaxLength(100);

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(p => new { p.EventId, p.WorkEmail }).IsUnique();
        builder.HasIndex(p => new { p.EventId, p.UserId }).IsUnique();
        builder.HasIndex(p => new { p.EventId, p.Status });

        builder.HasMany(p => p.Activities)
            .WithOne(a => a.Participant)
            .HasForeignKey(a => a.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.BadgeAwards)
            .WithOne(ba => ba.Participant)
            .HasForeignKey(ba => ba.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.ExternalConnections)
            .WithOne(ec => ec.Participant)
            .HasForeignKey(ec => ec.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Notifications)
            .WithOne(n => n.Participant)
            .HasForeignKey(n => n.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.TeamHistories)
            .WithOne(th => th.Participant)
            .HasForeignKey(th => th.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
