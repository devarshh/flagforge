using FlagForge.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlagForge.Infrastructure.Persistence.Configurations;

internal sealed class ScheduledChangeConfiguration : IEntityTypeConfiguration<ScheduledChange>
{
    public void Configure(EntityTypeBuilder<ScheduledChange> builder)
    {
        builder.ToTable("ScheduledChanges");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Action).HasConversion<string>().HasMaxLength(16).IsUnicode(false);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(16).IsUnicode(false);
        builder.Property(s => s.Payload).HasJsonConversion();
        builder.Property(s => s.Error).HasMaxLength(ScheduledChange.MaxErrorLength);
        builder.HasIndex(s => new { s.Status, s.ExecuteAt });
        builder.HasIndex(s => new { s.FlagId, s.EnvironmentId });
        builder.HasOne(s => s.Flag).WithMany().HasForeignKey(s => s.FlagId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.Environment).WithMany().HasForeignKey(s => s.EnvironmentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(s => s.CreatedBy).WithMany().HasForeignKey(s => s.CreatedByUserId).OnDelete(DeleteBehavior.NoAction);
    }
}
