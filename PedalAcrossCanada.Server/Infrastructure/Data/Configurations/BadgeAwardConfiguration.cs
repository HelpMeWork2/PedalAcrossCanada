using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PedalAcrossCanada.Server.Domain.Entities;

namespace PedalAcrossCanada.Server.Infrastructure.Data.Configurations;

public class BadgeAwardConfiguration : IEntityTypeConfiguration<BadgeAward>
{
    public void Configure(EntityTypeBuilder<BadgeAward> builder)
    {
        builder.HasKey(ba => ba.Id);
        builder.Property(ba => ba.Id).ValueGeneratedOnAdd();

        builder.Property(ba => ba.AwardedBy).HasMaxLength(450);

        builder.HasIndex(ba => new { ba.ParticipantId, ba.BadgeId, ba.EventId }).IsUnique();

        builder.HasOne(ba => ba.Badge)
            .WithMany(b => b.BadgeAwards)
            .HasForeignKey(ba => ba.BadgeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ba => ba.Event)
            .WithMany()
            .HasForeignKey(ba => ba.EventId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
