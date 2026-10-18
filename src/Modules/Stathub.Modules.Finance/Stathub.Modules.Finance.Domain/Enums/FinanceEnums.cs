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

/// <summary>Сторона матча, с которой выставлен взнос.</summary>
public enum ChargeSide
{
    Home = 0,
    Away = 1
}
