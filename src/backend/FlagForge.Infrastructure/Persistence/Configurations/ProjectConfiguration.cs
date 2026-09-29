using FlagForge.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlagForge.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Key).HasMaxLength(KeyFormat.MaxLength).IsUnicode(false);
        builder.HasIndex(p => p.Key).IsUnique();
        builder.Property(p => p.Name).HasMaxLength(Project.MaxNameLength);
        builder.Property(p => p.Description).HasMaxLength(Project.MaxDescriptionLength);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.CreatedByUserId).OnDelete(DeleteBehavior.NoAction);
    }
}
