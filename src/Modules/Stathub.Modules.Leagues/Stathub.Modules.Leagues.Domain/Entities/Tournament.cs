using Stathub.Modules.Leagues.Domain.Enums;
using Stathub.Modules.Leagues.Domain.ValueObjects;
using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Leagues.Domain.Entities;

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

    public static Tournament Create(Guid leagueId, string name, DateOnly startDate, DateOnly? endDate = null)
    {
        if (leagueId == Guid.Empty)
            throw new DomainException("League id is required.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Tournament name is required.");

        if (endDate is not null && endDate < startDate)
            throw new DomainException("Tournament end date cannot be before its start date.");

        return new Tournament(Guid.NewGuid(), leagueId, name.Trim(), startDate, endDate);
    }

    public void Start()
    {
        if (Status != TournamentStatus.Scheduled)
            throw new ConflictException($"Only a {TournamentStatus.Scheduled} tournament can be started.");

        Status = TournamentStatus.Active;
    }

    public void Complete()
    {
        if (Status != TournamentStatus.Active)
            throw new ConflictException($"Only an {TournamentStatus.Active} tournament can be completed.");

        Status = TournamentStatus.Completed;
    }

    public void Cancel()
    {
        if (Status == TournamentStatus.Completed)
            throw new ConflictException("A completed tournament cannot be cancelled.");

        Status = TournamentStatus.Cancelled;
    }

    public Stage AddStage(string name, StageFormatType formatType, PointsRule pointsRule, MatchFormatRule matchFormatRule)
    {
        if (Status != TournamentStatus.Scheduled)
            throw new ConflictException($"Stages can only be added to a {TournamentStatus.Scheduled} tournament.");

        var order = _stages.Count + 1;
        var stage = Stage.Create(Id, order, name, formatType, pointsRule, matchFormatRule);
        _stages.Add(stage);
        return stage;
    }
}
