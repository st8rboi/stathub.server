using Stathub.Modules.Finance.Domain.Enums;
using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Finance.Domain.Entities;

/// <summary>
/// Тариф взноса за матч с команды. Задаётся на турнир и может быть переопределён на этап
/// (в дивизионах часто разные взносы). Матч берёт тариф своего этапа, иначе турнира.
/// </summary>
public sealed class FeeSetting : AggregateRoot
{
    public FeeScopeType ScopeType { get; private set; }

    public Guid ScopeId { get; private set; }

    /// <summary>Сумма в рублях за матч с одной команды. Ноль — взносов нет.</summary>
    public int Amount { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    private FeeSetting()
    {
    }

    private FeeSetting(Guid id, FeeScopeType scopeType, Guid scopeId, int amount, Guid? updatedByUserId)
    {
        Id = id;
        ScopeType = scopeType;
        ScopeId = scopeId;
        Amount = amount;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public static FeeSetting Create(
        FeeScopeType scopeType,
        Guid scopeId,
        int amount,
        Guid? updatedByUserId = null)
    {
        if (!Enum.IsDefined(scopeType))
            throw new DomainException($"Неизвестный уровень тарифа '{scopeType}'.");
        if (scopeId == Guid.Empty)
            throw new DomainException("Не указан объект тарифа.");

        EnsureAmountValid(amount);

        return new FeeSetting(Guid.NewGuid(), scopeType, scopeId, amount, updatedByUserId);
    }

    public void ChangeAmount(int amount, Guid? updatedByUserId)
    {
        EnsureAmountValid(amount);

        Amount = amount;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static void EnsureAmountValid(int amount)
    {
        if (amount < 0)
            throw new DomainException("Взнос не может быть отрицательным.");
        if (amount > 1_000_000)
            throw new DomainException("Взнос выглядит подозрительно большим (больше 1 000 000).");
    }
}
