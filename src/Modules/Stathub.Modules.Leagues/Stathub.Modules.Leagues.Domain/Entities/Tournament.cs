using Stathub.Modules.Leagues.Domain.Enums;
using Stathub.Modules.Leagues.Domain.ValueObjects;
using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Leagues.Domain.Entities;

/// <summary>
/// Представляет турнир в рамках лиги, содержащий информацию о его статусе, датах проведения и этапах.
/// </summary>
public sealed class Tournament : AggregateRoot
{
    private readonly List<Stage> _stages = [];

    public Guid LeagueId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public TournamentStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<Stage> Stages => _stages.AsReadOnly();

    private Tournament()
    {
    }

    private Tournament(Guid id, Guid leagueId, string name, DateOnly startDate, DateOnly? endDate)
    {
        Id = id;
        LeagueId = leagueId;
        Name = name;
        StartDate = startDate;
        EndDate = endDate;
        Status = TournamentStatus.Scheduled;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Создает новый турнир в рамках лиги с указанными параметрами.
    /// </summary>
    /// <param name="leagueId"></param>
    /// <param name="name"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    /// <exception cref="DomainException"></exception>
    public static Tournament Create(Guid leagueId, string name, DateOnly startDate, DateOnly? endDate = null)
    {
        if (leagueId == Guid.Empty)
            throw new DomainException("League id обязательное поле.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Tournament name обязательное поле.");

        if (endDate is not null && endDate < startDate)
            throw new DomainException("Tournament end date не может быть раньше его даты начала.");

        return new Tournament(Guid.NewGuid(), leagueId, name.Trim(), startDate, endDate);
    }

    public void Start()
    {
        if (Status != TournamentStatus.Scheduled)
            throw new ConflictException($"Только турнир со статусом {TournamentStatus.Scheduled} может быть запущен.");

        Status = TournamentStatus.Active;
    }

    public void Complete()
    {
        if (Status != TournamentStatus.Active)
            throw new ConflictException($"Только турнир со статусом {TournamentStatus.Active} может быть завершен.");

        Status = TournamentStatus.Completed;
    }

    public void Cancel()
    {
        if (Status == TournamentStatus.Completed)
            throw new ConflictException($"Турнир со статусом {TournamentStatus.Completed} не может быть отменен.");

        Status = TournamentStatus.Cancelled;
    }

    public Stage AddStage(string name, StageFormatType formatType, PointsRule pointsRule, MatchFormatRule matchFormatRule)
    {
        if (Status != TournamentStatus.Scheduled)
            throw new ConflictException($"Стадия может быть добавлена только к турниру со статусом {TournamentStatus.Scheduled}.");

        var order = _stages.Count + 1;
        var stage = Stage.Create(Id, order, name, formatType, pointsRule, matchFormatRule);
        _stages.Add(stage);
        return stage;
    }
}
