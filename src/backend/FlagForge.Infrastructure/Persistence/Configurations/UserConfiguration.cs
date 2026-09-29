using FlagForge.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlagForge.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Property(u => u.Email).HasMaxLength(User.MaxEmailLength);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.DisplayName).HasMaxLength(User.MaxDisplayNameLength);
        builder.Property(u => u.PasswordHash).HasMaxLength(512).IsUnicode(false);
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(16).IsUnicode(false);
    }
}
