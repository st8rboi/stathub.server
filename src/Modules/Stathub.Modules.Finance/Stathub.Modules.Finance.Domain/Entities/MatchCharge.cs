using Stathub.Modules.Finance.Domain.Enums;
using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Finance.Domain.Entities;

/// <summary>
/// Начисление команде. Два вида происхождения:
/// <list type="bullet">
/// <item>взнос за матч (<see cref="ChargeKind.MatchFee"/>) — по одному на каждую команду матча,
/// сумма фиксируется в момент создания матча, изменение тарифа не переписывает прошлое;</item>
/// <item>ручное (вступительный взнос, штраф, прочее) — организатор выставляет команде в турнире,
/// этап необязателен, матча и стороны нет.</item>
/// </list>
/// Матч, этап и команда хранятся по <c>Guid</c> (межмодульные FK запрещены).
/// </summary>
public sealed class MatchCharge : AggregateRoot
{
    /// <summary>Матч — только у взноса за матч.</summary>
    public Guid? MatchId { get; private set; }

    /// <summary>Этап: у взноса за матч всегда, у ручного начисления — по желанию.</summary>
    public Guid? StageId { get; private set; }

    public Guid TournamentId { get; private set; }

    public Guid TeamId { get; private set; }

    /// <summary>Сторона матча — только у взноса за матч.</summary>
    public ChargeSide? Side { get; private set; }

    public ChargeKind Kind { get; private set; }

    /// <summary>Описание ручного начисления («Штраф за неявку 12.11»).</summary>
    public string? Description { get; private set; }

    public int Amount { get; private set; }

    public ChargeStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? PaidAtUtc { get; private set; }

    public PaymentMethod? PaymentMethod { get; private set; }

    public string? Comment { get; private set; }

    private MatchCharge()
    {
    }

    private MatchCharge(
        Guid id,
        Guid? matchId,
        Guid? stageId,
        Guid tournamentId,
        Guid teamId,
        ChargeSide? side,
        ChargeKind kind,
        string? description,
        int amount)
    {
        Id = id;
        MatchId = matchId;
        StageId = stageId;
        TournamentId = tournamentId;
        TeamId = teamId;
        Side = side;
        Kind = kind;
        Description = description;
        Amount = amount;
        Status = ChargeStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static MatchCharge Create(
        Guid matchId,
        Guid stageId,
        Guid tournamentId,
        Guid teamId,
        ChargeSide side,
        int amount)
    {
        if (matchId == Guid.Empty || stageId == Guid.Empty || teamId == Guid.Empty)
            throw new DomainException("Матч, этап и команда обязательны.");
        if (amount <= 0)
            throw new DomainException("Сумма начисления должна быть больше нуля.");

        return new MatchCharge(
            Guid.NewGuid(), matchId, stageId, tournamentId, teamId, side, ChargeKind.MatchFee, null, amount);
    }

    public const int DescriptionMaxLength = 200;

    /// <summary>
    /// Ручное начисление команде в турнире: вступительный взнос, штраф или прочее.
    /// Взнос за матч вручную не выставляется — он создаётся вместе с матчем.
    /// </summary>
    public static MatchCharge CreateManual(
        Guid tournamentId,
        Guid? stageId,
        Guid teamId,
        ChargeKind kind,
        int amount,
        string? description)
    {
        if (tournamentId == Guid.Empty || teamId == Guid.Empty)
            throw new DomainException("Турнир и команда обязательны.");
        if (stageId == Guid.Empty)
            throw new DomainException("Этап указан неверно.");
        if (kind == ChargeKind.MatchFee)
            throw new DomainException("Взнос за матч начисляется автоматически при создании матча.");
        if (!Enum.IsDefined(kind))
            throw new DomainException("Неизвестный вид начисления.");
        if (amount <= 0)
            throw new DomainException("Сумма начисления должна быть больше нуля.");

        var text = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (text is { Length: > DescriptionMaxLength })
            throw new DomainException($"Описание длиннее {DescriptionMaxLength} символов.");
        if (kind == ChargeKind.Other && text is null)
            throw new DomainException("Для прочего начисления укажите описание.");

        return new MatchCharge(Guid.NewGuid(), null, stageId, tournamentId, teamId, null, kind, text, amount);
    }

    /// <summary>Организатор отметил оплату: взнос получен наличными, переводом или безналом.</summary>
    public void MarkPaid(PaymentMethod method, string? comment)
    {
        if (Status == ChargeStatus.Cancelled)
            throw new ConflictException("Взнос отменён: отметить оплату нельзя.");
        if (Status == ChargeStatus.Paid)
            throw new ConflictException("Взнос уже оплачен.");

        Status = ChargeStatus.Paid;
        PaidAtUtc = DateTime.UtcNow;
        PaymentMethod = method;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    }

    /// <summary>
    /// В матче заменили команду: долг переходит к новой. Оплаченный взнос не переносится —
    /// деньги уже получены от прежней команды.
    /// </summary>
    public void ChangeTeam(Guid teamId)
    {
        if (teamId == Guid.Empty)
            throw new DomainException("Команда обязательна.");

        if (teamId == TeamId)
            return;

        EnsureNotPaid("Взнос по матчу уже оплачен прежней командой: заменить команду нельзя.");
        TeamId = teamId;
    }

    /// <summary>Удаляется ошибочно созданный матч: оплаченный взнос удалять нельзя.</summary>
    public void EnsureCanRemove() =>
        EnsureNotPaid("По матчу есть оплаченный взнос: удалить матч нельзя.");

    private void EnsureNotPaid(string message)
    {
        if (Status == ChargeStatus.Paid)
            throw new ConflictException(message);
    }

    /// <summary>Отмена начисления (например, матч отменён). Оплаченный взнос не отменяется.</summary>
    public void Cancel(string? comment)
    {
        if (Status == ChargeStatus.Paid)
            throw new ConflictException("Оплаченный взнос отменить нельзя.");

        Status = ChargeStatus.Cancelled;
        Comment = string.IsNullOrWhiteSpace(comment) ? Comment : comment.Trim();
    }
}
