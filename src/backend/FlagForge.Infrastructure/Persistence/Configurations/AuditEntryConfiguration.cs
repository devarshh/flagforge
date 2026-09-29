using FlagForge.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlagForge.Infrastructure.Persistence.Configurations;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).UseIdentityColumn();
        builder.Property(a => a.ActorType).HasConversion<string>().HasMaxLength(16).IsUnicode(false);
        builder.Property(a => a.ActorName).HasMaxLength(256);
        builder.Property(a => a.Action).HasMaxLength(64).IsUnicode(false);
        builder.Property(a => a.ResourceKey).HasMaxLength(300);
        builder.Property(a => a.Comment).HasMaxLength(AuditEntry.MaxCommentLength);
        builder.Property(a => a.Before).HasJsonConversion();
        builder.Property(a => a.After).HasJsonConversion();
        builder.HasIndex(a => new { a.ProjectId, a.OccurredAt }).IsDescending(false, true);
        builder.HasIndex(a => new { a.FlagId, a.OccurredAt }).IsDescending(false, true);
        builder.HasIndex(a => a.OccurredAt).IsDescending();
    }
}
