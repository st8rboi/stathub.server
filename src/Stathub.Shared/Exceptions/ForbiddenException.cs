namespace Stathub.Shared.Exceptions;

/// <summary>
/// Действие запрещено: пользователь аутентифицирован, но прав на объект не имеет.
/// Отличается от 404 тем, что объект существует, и от 400 — тем, что запрос корректен.
/// </summary>
public sealed class ForbiddenException(string message) : Exception(message);
