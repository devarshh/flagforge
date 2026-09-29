using FlagForge.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlagForge.Infrastructure.Persistence.Configurations;

internal sealed class SdkKeyConfiguration : IEntityTypeConfiguration<SdkKey>
{
    public void Configure(EntityTypeBuilder<SdkKey> builder)
    {
        builder.ToTable("SdkKeys");
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Id).ValueGeneratedNever();
        builder.Property(k => k.Name).HasMaxLength(SdkKey.MaxNameLength);
        builder.Property(k => k.KeyPrefix).HasMaxLength(SdkKey.DisplayPrefixLength).IsUnicode(false);
        builder.Property(k => k.KeyHash).HasMaxLength(64).IsUnicode(false).IsFixedLength();
        builder.HasIndex(k => k.KeyHash).IsUnique();
        builder.HasOne(k => k.Environment).WithMany().HasForeignKey(k => k.EnvironmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(k => k.CreatedByUserId).OnDelete(DeleteBehavior.NoAction);
    }
}
