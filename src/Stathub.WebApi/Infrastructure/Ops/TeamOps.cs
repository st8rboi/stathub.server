using Stathub.Infrastructure.Storage;
using Stathub.Modules.Teams.Application;
using Stathub.Shared.Exceptions;

namespace Stathub.WebApi.Infrastructure.Ops;

/// <summary>
/// Служебная команда техподдержки: снимает логотип команды вместе с закреплением за организацией,
/// после чего новый логотип может загрузить любой организатор.
/// <para>Пример: <c>dotnet run -- reset-team-logo 3fa85f64-5717-4562-b3fc-2c963f66afa6</c></para>
/// </summary>
internal static class TeamOps
{
    private const string Command = "reset-team-logo";

    public static bool Matches(string[] args) =>
        args.Length > 0 && string.Equals(args[0], Command, StringComparison.OrdinalIgnoreCase);

    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        if (args.Length < 2 || !Guid.TryParse(args[1], out var teamId))
        {
            Console.Error.WriteLine($"Ошибка: укажите Id команды: {Command} <guid>");
            return 1;
        }

        try
        {
            using var scope = services.CreateScope();

            var previousLogoUrl = await scope.ServiceProvider
                .GetRequiredService<TeamService>()
                .ResetLogoAsync(teamId);

            await scope.ServiceProvider
                .GetRequiredService<ILogoStorage>()
                .DeleteAsync(previousLogoUrl);

            Console.WriteLine($"Логотип команды {teamId} сброшен.");
            return 0;
        }
        catch (NotFoundException exception)
        {
            Console.Error.WriteLine($"Ошибка: {exception.Message}");
            return 1;
        }
    }
}
