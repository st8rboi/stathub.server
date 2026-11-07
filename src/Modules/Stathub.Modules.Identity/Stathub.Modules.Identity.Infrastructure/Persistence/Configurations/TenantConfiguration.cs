using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stathub.Modules.Identity.Domain.Entities;

namespace Stathub.Modules.Identity.Infrastructure.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(tenant => tenant.Id);
        builder.Property(tenant => tenant.Id).ValueGeneratedNever();

        builder.Property(tenant => tenant.Name).IsRequired().HasMaxLength(150);
        builder.Property(tenant => tenant.Slug).IsRequired().HasMaxLength(64);
        builder.Property(tenant => tenant.Status).IsRequired();
        builder.Property(tenant => tenant.CreatedAtUtc).IsRequired();

        builder.HasIndex(tenant => tenant.Slug).IsUnique();
    }
}
