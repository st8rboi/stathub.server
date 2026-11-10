using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stathub.Modules.Finance.Domain.Entities;
using Stathub.Modules.Finance.Domain.Enums;

namespace Stathub.Modules.Finance.Infrastructure.Persistence;

public sealed class FinanceDbContext(DbContextOptions<FinanceDbContext> options) : DbContext(options)
{
    public DbSet<FeeSetting> FeeSettings => Set<FeeSetting>();
    public DbSet<MatchCharge> MatchCharges => Set<MatchCharge>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("finance");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceDbContext).Assembly);
    }
}

internal sealed class FeeSettingConfiguration : IEntityTypeConfiguration<FeeSetting>
{
    public void Configure(EntityTypeBuilder<FeeSetting> builder)
    {
        builder.ToTable("fee_settings");

        builder.HasKey(setting => setting.Id);
        builder.Property(setting => setting.Id).ValueGeneratedNever();
        builder.Property(setting => setting.ScopeType).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(setting => setting.ScopeId).IsRequired();
        builder.Property(setting => setting.Amount).IsRequired();
        builder.Property(setting => setting.UpdatedAtUtc).IsRequired();
        builder.Property(setting => setting.UpdatedByUserId);

        builder.HasIndex(setting => new { setting.ScopeType, setting.ScopeId }).IsUnique();
    }
}

internal sealed class MatchChargeConfiguration : IEntityTypeConfiguration<MatchCharge>
{
    public void Configure(EntityTypeBuilder<MatchCharge> builder)
    {
        builder.ToTable("match_charges");

        builder.HasKey(charge => charge.Id);
        builder.Property(charge => charge.Id).ValueGeneratedNever();
        // Матч, сторона и этап есть только у взноса за матч; у ручного начисления этап необязателен.
        builder.Property(charge => charge.MatchId);
        builder.Property(charge => charge.StageId);
        builder.Property(charge => charge.TournamentId).IsRequired();
        builder.Property(charge => charge.TeamId).IsRequired();
        builder.Property(charge => charge.Side).HasConversion<string>().HasMaxLength(10);
        // Значение по умолчанию — для строк, созданных до появления видов: все они взносы за матч.
        builder.Property(charge => charge.Kind)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(ChargeKind.MatchFee);
        builder.Property(charge => charge.Description).HasMaxLength(MatchCharge.DescriptionMaxLength);
        builder.Property(charge => charge.Amount).IsRequired();
        builder.Property(charge => charge.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(charge => charge.CreatedAtUtc).IsRequired();
        builder.Property(charge => charge.PaidAtUtc);
        builder.Property(charge => charge.PaymentMethod).HasConversion<string>().HasMaxLength(20);
        builder.Property(charge => charge.Comment).HasMaxLength(500);

        builder.HasIndex(charge => charge.StageId);
        builder.HasIndex(charge => charge.TournamentId);
        builder.HasIndex(charge => new { charge.MatchId, charge.Side }).IsUnique();
    }
}
