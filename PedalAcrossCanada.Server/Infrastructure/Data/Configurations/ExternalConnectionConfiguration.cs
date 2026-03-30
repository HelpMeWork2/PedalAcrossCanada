using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PedalAcrossCanada.Server.Domain.Entities;

namespace PedalAcrossCanada.Server.Infrastructure.Data.Configurations;

public class ExternalConnectionConfiguration : IEntityTypeConfiguration<ExternalConnection>
{
    public void Configure(EntityTypeBuilder<ExternalConnection> builder)
    {
        builder.HasKey(ec => ec.Id);
        builder.Property(ec => ec.Id).ValueGeneratedOnAdd();

        builder.Property(ec => ec.Provider).IsRequired().HasMaxLength(50);
        builder.Property(ec => ec.ExternalAthleteId).IsRequired().HasMaxLength(100);
        builder.Property(ec => ec.EncryptedTokenData).IsRequired();

        builder.Property(ec => ec.ConnectionStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(ec => new { ec.ParticipantId, ec.Provider }).IsUnique();
    }
}
