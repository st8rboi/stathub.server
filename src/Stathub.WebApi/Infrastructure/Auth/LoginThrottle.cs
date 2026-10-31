using System.Net;
using Microsoft.Extensions.Caching.Memory;

namespace Stathub.WebApi.Infrastructure.Auth;

/// <summary>
/// Защита входа от подбора пароля: после 5 неудач по паре «логин + IP» вход с этой пары
/// закрыт на 15 минут. Пара, а не один логин: иначе кто угодно мог бы заблокировать судью
/// перед матчем, просто зная его логин. Общий предел запросов с одного IP — в политике
/// <see cref="IpPolicy"/> (Program.cs). Счётчики живут в памяти и обнуляются при перезапуске.
/// </summary>
public sealed class LoginThrottle : IDisposable
{
    public const string IpPolicy = "login-ip";
    public const int DefaultIpLimitPerMinute = 30;
    public const int MaxFailures = 5;

    private const int MaxLoginKeyLength = 200;
    private const int MaxTrackedPairs = 100_000;

    private static readonly TimeSpan BlockDuration = TimeSpan.FromMinutes(15);

    // Предел записей: перебор случайных логинов не должен съедать память сервера.
    private readonly MemoryCache _attempts = new(new MemoryCacheOptions { SizeLimit = MaxTrackedPairs });
    private readonly Lock _gate = new();

    /// <summary>
    /// Резервирует попытку до проверки пароля: параллельные запросы не успеют проскочить,
    /// пока BCrypt считает хеш. Успешный вход снимает счётчик через <see cref="Reset"/>.
    /// </summary>
    public bool TryBeginAttempt(string login, IPAddress? ip, out TimeSpan retryAfter)
    {
        var key = BuildKey(login, ip);
        var now = DateTimeOffset.UtcNow;

        lock (_gate)
        {
            var current = _attempts.Get<AttemptCounter>(key);

            if (current is { Count: >= MaxFailures })
            {
                retryAfter = current.ExpiresAt - now;
                return false;
            }

            Store(key, new AttemptCounter((current?.Count ?? 0) + 1, now + BlockDuration));
            retryAfter = TimeSpan.Zero;
            return true;
        }
    }

    public void Reset(string login, IPAddress? ip)
    {
        lock (_gate)
            _attempts.Remove(BuildKey(login, ip));
    }

    /// <summary>Сбой сервера — не неудачный вход: попытка возвращается, чтобы не блокировать человека.</summary>
    public void ReleaseAttempt(string login, IPAddress? ip)
    {
        var key = BuildKey(login, ip);

        lock (_gate)
        {
            var current = _attempts.Get<AttemptCounter>(key);

            if (current is null)
                return;

            if (current.Count <= 1)
                _attempts.Remove(key);
            else
                Store(key, current with { Count = current.Count - 1 });
        }
    }

    public void Dispose() => _attempts.Dispose();

    private void Store(string key, AttemptCounter counter) =>
        _attempts.Set(key, counter, new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = counter.ExpiresAt,
            Size = 1
        });

    private static string BuildKey(string login, IPAddress? ip)
    {
        var normalized = login.Trim().ToLowerInvariant();

        if (normalized.Length > MaxLoginKeyLength)
            normalized = normalized[..MaxLoginKeyLength];

        return $"{ip}|{normalized}";
    }

    private sealed record AttemptCounter(int Count, DateTimeOffset ExpiresAt);
}
