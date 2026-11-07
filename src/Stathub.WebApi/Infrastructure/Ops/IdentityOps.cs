using Stathub.Modules.Identity.Application;
using Stathub.Shared.Exceptions;

namespace Stathub.WebApi.Infrastructure.Ops;

/// <summary>
/// Служебные команды без HTTP: платформа создаёт организацию и её владельца.
/// Публичной регистрации нет, поэтому первый аккаунт появляется только так.
/// <para>Пример: <c>dotnet run -- create-organizer --tenant "ЛФЛ Пермь" --login owner --password Secret12345 --name "Иван Организаторов"</c></para>
/// </summary>
internal static class IdentityOps
{
    private const string Command = "create-organizer";

    public static bool Matches(string[] args) =>
        args.Length > 0 && string.Equals(args[0], Command, StringComparison.OrdinalIgnoreCase);

    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        var options = ParseOptions(args);

        try
        {
            using var scope = services.CreateScope();
            var accounts = scope.ServiceProvider.GetRequiredService<AccountService>();

            var (tenant, user) = await accounts.CreateTenantWithOwnerAsync(
                tenantName: Require(options, "tenant"),
                tenantSlug: options.GetValueOrDefault("slug"),
                login: Require(options, "login"),
                displayName: Require(options, "name"),
                password: Require(options, "password"),
                email: options.GetValueOrDefault("email"));

            Console.WriteLine($"Организация создана: {tenant.Name} (slug={tenant.Slug}, id={tenant.Id})");
            Console.WriteLine($"Организатор создан: {user.Login} (id={user.Id})");
            Console.WriteLine("Вход: POST /auth/login { login, password }");

            return 0;
        }
        catch (Exception exception) when (exception is DomainException or ConflictException or NotFoundException)
        {
            Console.Error.WriteLine($"Ошибка: {exception.Message}");
            return 1;
        }
    }

    private static string Require(IReadOnlyDictionary<string, string> options, string name) =>
        options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new DomainException($"Не указан обязательный параметр --{name}.");

    /// <summary>Разбор `--key value` (и `--key=value`), значения с пробелами — в кавычках на стороне оболочки.</summary>
    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
                continue;

            var key = argument[2..];
            var separator = key.IndexOf('=');

            if (separator >= 0)
            {
                options[key[..separator]] = key[(separator + 1)..];
                continue;
            }

            if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                options[key] = args[index + 1];
                index++;
            }
        }

        return options;
    }
}
