using Stathub.Shared.Exceptions;

namespace Stathub.Modules.Leagues.Domain.ValueObjects;

public sealed record PointsRule
{
    public int WinPoints { get; }
    public int DrawPoints { get; }
    public int LossPoints { get; }

    public PointsRule(int winPoints, int drawPoints, int lossPoints)
    {
        if (winPoints < 0)
            throw new DomainException("Win points cannot be negative.");

        if (drawPoints < 0)
            throw new DomainException("Draw points cannot be negative.");

        if (lossPoints < 0)
            throw new DomainException("Loss points cannot be negative.");

        WinPoints = winPoints;
        DrawPoints = drawPoints;
        LossPoints = lossPoints;
    }
}
