using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PedalAcrossCanada.Server.Domain.Entities;

namespace PedalAcrossCanada.Server.Infrastructure.Data.Configurations;

public class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedOnAdd();

        builder.Property(a => a.Notes).HasMaxLength(500);
        builder.Property(a => a.ExternalActivityId).HasMaxLength(100);
        builder.Property(a => a.ExternalTitle).HasMaxLength(500);
        builder.Property(a => a.ApprovedBy).HasMaxLength(450);
        builder.Property(a => a.RejectedBy).HasMaxLength(450);
        builder.Property(a => a.RejectionReason).HasMaxLength(500);

        builder.Property(a => a.Source)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(a => a.RideType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(a => new { a.ParticipantId, a.ExternalActivityId })
            .IsUnique()
            .HasFilter("[ExternalActivityId] IS NOT NULL");

        builder.HasIndex(a => new { a.EventId, a.Status });
        builder.HasIndex(a => new { a.ParticipantId, a.ActivityDate });

        builder.HasOne(a => a.DuplicateOfActivity)
            .WithMany(a => a.DuplicateActivities)
            .HasForeignKey(a => a.DuplicateOfActivityId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
