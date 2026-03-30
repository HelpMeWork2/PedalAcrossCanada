using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PedalAcrossCanada.Server.Domain.Entities;

namespace PedalAcrossCanada.Server.Infrastructure.Data.Configurations;

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedOnAdd();

        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Description).HasMaxLength(500);

        builder.HasIndex(t => new { t.EventId, t.Name }).IsUnique();

        builder.HasOne(t => t.Captain)
            .WithMany()
            .HasForeignKey(t => t.CaptainParticipantId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasMany(t => t.Participants)
            .WithOne(p => p.Team)
            .HasForeignKey(p => p.TeamId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(t => t.TeamHistories)
            .WithOne(th => th.Team)
            .HasForeignKey(th => th.TeamId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
