namespace Stathub.Shared.Exceptions;

/// <summary>Нарушение бизнес-правила или некорректные входные данные (HTTP 400).</summary>
public class DomainException(string message) : Exception(message);
