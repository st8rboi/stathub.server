using Stathub.Modules.Finance.Domain.Enums;
using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Finance.Domain.Entities;

/// <summary>
/// Начисление взноса за матч: по одному на каждую команду. Сумма фиксируется в момент
/// создания матча — изменение тарифа не переписывает прошлое. Матч и команда хранятся
/// по <c>Guid</c> (межмодульные FK запрещены).
/// </summary>
public sealed class MatchCharge : AggregateRoot
{
    public Guid MatchId { get; private set; }

    public Guid StageId { get; private set; }

    public Guid TournamentId { get; private set; }

    public Guid TeamId { get; private set; }

    public ChargeSide Side { get; private set; }

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
        Guid matchId,
        Guid stageId,
        Guid tournamentId,
        Guid teamId,
        ChargeSide side,
        int amount)
    {
        Id = id;
        MatchId = matchId;
        StageId = stageId;
        TournamentId = tournamentId;
        TeamId = teamId;
        Side = side;
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

        return new MatchCharge(Guid.NewGuid(), matchId, stageId, tournamentId, teamId, side, amount);
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
