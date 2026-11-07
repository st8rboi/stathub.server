using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stathub.Modules.Identity.Domain.Entities;

namespace Stathub.Modules.Identity.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();

        builder.Property(user => user.Login).IsRequired().HasMaxLength(64);
        builder.Property(user => user.Email).HasMaxLength(254);
        builder.Property(user => user.DisplayName).IsRequired().HasMaxLength(100);
        builder.Property(user => user.PasswordHash).IsRequired().HasMaxLength(200);
        builder.Property(user => user.IsActive).IsRequired();
        builder.Property(user => user.CreatedAtUtc).IsRequired();
        builder.Property(user => user.LastLoginAtUtc);

        builder.HasIndex(user => user.Login).IsUnique();
    }
}
