using FlagForge.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlagForge.Infrastructure.Persistence.Configurations;

internal sealed class FlagEnvironmentConfigConfiguration : IEntityTypeConfiguration<FlagEnvironmentConfig>
{
    public void Configure(EntityTypeBuilder<FlagEnvironmentConfig> builder)
    {
        builder.ToTable("FlagEnvironmentConfigs");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.HasIndex(c => new { c.FlagId, c.EnvironmentId }).IsUnique();
        builder.HasIndex(c => c.EnvironmentId);
        builder.Property(c => c.OffVariationId).HasMaxLength(64).IsUnicode(false);
        builder.Property(c => c.Targets).HasJsonConversion();
        builder.Property(c => c.Rules).HasJsonConversion();
        builder.Property(c => c.Fallthrough).HasJsonConversion();
        builder.Property(c => c.Version).IsConcurrencyToken();

        // Configs are reachable from a project through both flags and environments; SQL Server allows only one
        // cascade path, so deleting an environment removes its configs explicitly.
        builder.HasOne(c => c.Flag).WithMany(f => f.Configs).HasForeignKey(c => c.FlagId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(c => c.Environment).WithMany().HasForeignKey(c => c.EnvironmentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.UpdatedByUserId).OnDelete(DeleteBehavior.NoAction);
    }
}
