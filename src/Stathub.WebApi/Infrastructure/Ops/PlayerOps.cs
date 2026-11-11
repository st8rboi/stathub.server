using Stathub.Infrastructure.Storage;
using Stathub.Modules.Players.Application;
using Stathub.Shared.Exceptions;

namespace Stathub.WebApi.Infrastructure.Ops;

/// <summary>
/// Служебная команда техподдержки: снимает фото игрока вместе с закреплением за организацией,
/// после чего новое фото может загрузить любой организатор.
/// <para>Пример: <c>dotnet run -- reset-player-photo 3fa85f64-5717-4562-b3fc-2c963f66afa6</c></para>
/// </summary>
internal static class PlayerOps
{
    private const string Command = "reset-player-photo";

    public static bool Matches(string[] args) =>
        args.Length > 0 && string.Equals(args[0], Command, StringComparison.OrdinalIgnoreCase);

    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        if (args.Length < 2 || !Guid.TryParse(args[1], out var playerId))
        {
            Console.Error.WriteLine($"Ошибка: укажите Id игрока: {Command} <guid>");
            return 1;
        }

        try
        {
            using var scope = services.CreateScope();

            var previousPhotoUrl = await scope.ServiceProvider
                .GetRequiredService<PlayerService>()
                .ResetPhotoAsync(playerId);

            await scope.ServiceProvider
                .GetRequiredService<ILogoStorage>()
                .DeleteAsync(previousPhotoUrl);

            Console.WriteLine($"Фото игрока {playerId} сброшено.");
            return 0;
        }
        catch (NotFoundException exception)
        {
            Console.Error.WriteLine($"Ошибка: {exception.Message}");
            return 1;
        }
    }
}
