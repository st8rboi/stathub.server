using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stathub.Modules.Finance.Application;
using Stathub.Modules.Finance.Application.Interfaces;
using Stathub.Modules.Finance.Infrastructure.Persistence;

namespace Stathub.Modules.Finance.Infrastructure;

public static class FinanceModule
{
    public static IServiceCollection AddFinanceModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Строка подключения 'Postgres' не настроена");

        services.AddDbContext<FinanceDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IFeeSettingRepository, FeeSettingRepository>();
        services.AddScoped<IMatchChargeRepository, MatchChargeRepository>();
        services.AddScoped<ITeamNameSource, TeamNameSource>();
        services.AddScoped<FeeService>();

        return services;
    }
}
