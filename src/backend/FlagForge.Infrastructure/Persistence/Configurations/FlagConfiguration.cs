using FlagForge.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlagForge.Infrastructure.Persistence.Configurations;

internal sealed class FlagConfiguration : IEntityTypeConfiguration<Flag>
{
    public void Configure(EntityTypeBuilder<Flag> builder)
    {
        builder.ToTable("Flags");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.Property(f => f.Key).HasMaxLength(KeyFormat.MaxLength).IsUnicode(false);
        builder.HasIndex(f => new { f.ProjectId, f.Key }).IsUnique();
        builder.Property(f => f.Name).HasMaxLength(Flag.MaxNameLength);
        builder.Property(f => f.Description).HasMaxLength(Flag.MaxDescriptionLength);
        builder.Property(f => f.Type).HasConversion<string>().HasMaxLength(16).IsUnicode(false);
        builder.Property(f => f.Variations).HasJsonConversion();

        // Tags use EF Core's primitive-collection JSON mapping, so the tag filter translates to OPENJSON in SQL.
        builder.PrimitiveCollection(f => f.Tags).HasColumnType("nvarchar(max)").ElementType().HasMaxLength(Flag.MaxTagLength);

        builder.Property(f => f.Salt).HasMaxLength(16).IsUnicode(false).IsFixedLength();
        builder.Ignore(f => f.VariationIds);
        builder.HasOne(f => f.Project).WithMany(p => p.Flags).HasForeignKey(f => f.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(f => f.CreatedByUserId).OnDelete(DeleteBehavior.NoAction);
    }
}
