namespace Stathub.Shared.Domain;

public abstract class Entity
{
    public Guid Id { get; protected init; }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity other || other.GetType() != GetType())
            return false;

        return Id == other.Id;
    }

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity? left, Entity? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Entity? left, Entity? right) => !(left == right);
}
