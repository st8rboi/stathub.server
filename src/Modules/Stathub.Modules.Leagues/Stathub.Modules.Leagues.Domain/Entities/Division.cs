using Stathub.Shared.Domain;
using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Leagues.Domain.Entities;

/// <summary>
/// Дивизион — независимое соревнование внутри турнира: свои команды, заявки, статистика и чемпион.
/// Этапы (круг, плей-офф) живут внутри дивизиона. У простого турнира дивизион один,
/// и интерфейс его не показывает.
/// </summary>
public sealed class Division : Entity
{
    public Guid TournamentId { get; private set; }
    public int Order { get; private set; }
    public string Name { get; private set; } = string.Empty;

    private Division()
    {
    }

    private Division(Guid id, Guid tournamentId, int order, string name)
    {
        Id = id;
        TournamentId = tournamentId;
        Order = order;
        Name = name;
    }

    internal static Division Create(Guid tournamentId, int order, string name) =>
        new(Guid.NewGuid(), tournamentId, order, NormalizeName(name));

    internal void Rename(string name) => Name = NormalizeName(name);

    internal void SetOrder(int order) => Order = order;

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Название дивизиона обязательное поле.");

        return name.Trim();
    }
}
