namespace Stathub.Shared.Exceptions;

/// <summary>Операция конфликтует с текущим состоянием: дубликат, недопустимый переход статуса (HTTP 409).</summary>
public class ConflictException(string message) : Exception(message);
