using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PedalAcrossCanada.Server.Domain.Entities;

namespace PedalAcrossCanada.Server.Infrastructure.Data.Configurations;

public class TeamHistoryConfiguration : IEntityTypeConfiguration<TeamHistory>
{
    public void Configure(EntityTypeBuilder<TeamHistory> builder)
    {
        builder.HasKey(th => th.Id);
        builder.Property(th => th.Id).ValueGeneratedOnAdd();

        builder.HasIndex(th => new { th.ParticipantId, th.EffectiveFrom });
    }
}
