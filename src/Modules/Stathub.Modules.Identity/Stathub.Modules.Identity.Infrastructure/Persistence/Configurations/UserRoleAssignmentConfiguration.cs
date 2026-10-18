using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stathub.Modules.Identity.Domain.Entities;

namespace Stathub.Modules.Identity.Infrastructure.Persistence.Configurations;

internal sealed class UserRoleAssignmentConfiguration : IEntityTypeConfiguration<UserRoleAssignment>
{
    public void Configure(EntityTypeBuilder<UserRoleAssignment> builder)
    {
        builder.ToTable("user_role_assignments");

        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Id).ValueGeneratedNever();

        builder.Property(assignment => assignment.UserId).IsRequired();
        builder.Property(assignment => assignment.TenantId).IsRequired();
        // Enum-ы строками: читаемость в БД важнее пары байт, а порядок значений может меняться.
        builder.Property(assignment => assignment.Role).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(assignment => assignment.ScopeType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(assignment => assignment.ScopeId);
        builder.Property(assignment => assignment.GrantedByUserId);
        builder.Property(assignment => assignment.GrantedAtUtc).IsRequired();
        builder.Property(assignment => assignment.RevokedAtUtc);

        builder.HasIndex(assignment => new { assignment.UserId, assignment.TenantId });
        builder.HasIndex(assignment => assignment.TenantId);

        // Активное назначение не должно дублироваться; отозванные остаются в истории.
        builder
            .HasIndex(assignment => new
            {
                assignment.UserId,
                assignment.TenantId,
                assignment.Role,
                assignment.ScopeType,
                assignment.ScopeId
            })
            .IsUnique()
            .HasFilter("\"RevokedAtUtc\" IS NULL")
            .HasDatabaseName("ux_user_role_assignments_active");
    }
}
