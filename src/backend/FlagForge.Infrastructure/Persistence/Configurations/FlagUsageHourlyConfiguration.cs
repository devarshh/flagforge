using FlagForge.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlagForge.Infrastructure.Persistence.Configurations;

internal sealed class FlagUsageHourlyConfiguration : IEntityTypeConfiguration<FlagUsageHourly>
{
    public void Configure(EntityTypeBuilder<FlagUsageHourly> builder)
    {
        builder.ToTable("FlagUsageHourly");
        builder.HasKey(u => new { u.EnvironmentId, u.FlagId, u.VariationId, u.HourStart });
        builder.Property(u => u.VariationId).HasMaxLength(64).IsUnicode(false);
        builder.HasIndex(u => new { u.FlagId, u.HourStart });
        builder.HasIndex(u => u.HourStart);
    }
}
