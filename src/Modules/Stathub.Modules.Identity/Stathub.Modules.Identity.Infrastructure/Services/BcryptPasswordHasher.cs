using Stathub.Modules.Identity.Application.Interfaces;

namespace Stathub.Modules.Identity.Infrastructure.Services;

/// <summary>
/// BCrypt с рабочим фактором 12: ~100 мс на проверку — заметно для перебора, но незаметно
/// для входа. Пакет BCrypt.Net-Next уже закреплён в Directory.Packages.props.
/// </summary>
internal sealed class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Битая запись в БД не должна превращаться в 500 при входе.
            return false;
        }
    }
}
