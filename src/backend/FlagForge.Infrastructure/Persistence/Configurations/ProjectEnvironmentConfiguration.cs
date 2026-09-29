using FlagForge.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlagForge.Infrastructure.Persistence.Configurations;

internal sealed class ProjectEnvironmentConfiguration : IEntityTypeConfiguration<ProjectEnvironment>
{
    public void Configure(EntityTypeBuilder<ProjectEnvironment> builder)
    {
        builder.ToTable("Environments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Key).HasMaxLength(KeyFormat.MaxLength).IsUnicode(false);
        builder.HasIndex(e => new { e.ProjectId, e.Key }).IsUnique();
        builder.Property(e => e.Name).HasMaxLength(ProjectEnvironment.MaxNameLength);
        builder.Property(e => e.Color).HasMaxLength(7).IsUnicode(false).IsFixedLength();
        builder.HasOne(e => e.Project).WithMany(p => p.Environments).HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
