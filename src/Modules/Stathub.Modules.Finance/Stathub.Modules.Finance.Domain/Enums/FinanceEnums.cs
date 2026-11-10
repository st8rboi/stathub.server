namespace Stathub.Modules.Finance.Domain.Enums;

/// <summary>На что задан тариф взноса: турнир целиком или отдельный этап (дивизион).</summary>
public enum FeeScopeType
{
    Tournament = 0,
    Stage = 1
}

/// <summary>Состояние начисления за матч.</summary>
public enum ChargeStatus
{
    /// <summary>Взнос выставлен, оплаты ещё нет.</summary>
    Pending = 0,

    /// <summary>Организатор отметил оплату.</summary>
    Paid = 1,

    /// <summary>Матч отменён — взнос не действует.</summary>
    Cancelled = 2
}

/// <summary>Как заплатили: наличные, перевод или безнал.</summary>
public enum PaymentMethod
{
    Cash = 0,
    Transfer = 1,
    Bank = 2
}

/// <summary>
/// Вид начисления. Взнос за матч создаётся автоматически при создании матча,
/// остальные организатор выставляет команде вручную.
/// </summary>
public enum ChargeKind
{
    /// <summary>Взнос за матч по тарифу турнира/этапа.</summary>
    MatchFee = 0,

    /// <summary>Вступительный (заявочный) взнос команды.</summary>
    EntryFee = 1,

    /// <summary>Штраф (неявка, нарушения и т. п.).</summary>
    Fine = 2,

    /// <summary>Прочее — смысл в описании.</summary>
    Other = 3
}

/// <summary>Сторона матча, с которой выставлен взнос.</summary>
public enum ChargeSide
{
    Home = 0,
    Away = 1
}
