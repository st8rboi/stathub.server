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

    /// <summary>Изменение названия и дат турнира.</summary>
    public void Update(string name, DateOnly startDate, DateOnly? endDate)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Название турнира обязательное поле.");

        if (endDate is not null && endDate < startDate)
            throw new DomainException("Дата окончания турнира не может быть раньше даты начала.");

        Name = name.Trim();
        StartDate = startDate;
        EndDate = endDate;
    }

    /// <summary>
    /// Изменение этапа. Название меняется всегда. Правила — только пока турнир не начат и
    /// в этапе нет матчей: матч копирует формат при создании, а очки сразу пересчитывают таблицу.
    /// </summary>
    public Stage UpdateStage(
        Guid stageId,
        string name,
        StageFormatType formatType,
        PointsRule pointsRule,
        MatchFormatRule matchFormatRule,
        bool stageHasMatches)
    {
        var stage = GetStage(stageId);

        if (stage.HasDifferentRules(formatType, pointsRule, matchFormatRule))
        {
            if (Status != TournamentStatus.Scheduled)
                throw new ConflictException(
                    "Правила этапа можно менять только до начала турнира. Изменить можно только название.");

            if (stageHasMatches)
                throw new ConflictException(
                    "Правила этапа нельзя менять: в нём уже есть матчи. Изменить можно только название.");
        }

        stage.Update(name, formatType, pointsRule, matchFormatRule);
        return stage;
    }

    /// <summary>
    /// Удаление этапа. Проверка ссылок (заявки, матчи) — на вызывающем: они живут в других модулях.
    /// Оставшиеся этапы перенумеровываются, чтобы порядок шёл без пропусков.
    /// </summary>
    public void RemoveStage(Guid stageId)
    {
        var stage = GetStage(stageId);

        _stages.Remove(stage);

        var order = 1;
        foreach (var remaining in _stages.OrderBy(s => s.Order))
            remaining.SetOrder(order++);
    }

    private Stage GetStage(Guid stageId) =>
        _stages.FirstOrDefault(s => s.Id == stageId)
        ?? throw new NotFoundException($"Этап '{stageId}' не найден в турнире '{Id}'.");

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
