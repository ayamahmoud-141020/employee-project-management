namespace EPM.Domain.Abstractions;

/// <summary>
/// Anything with a database identity and a lifecycle of its own.
/// </summary>
public abstract class Entity
{
    // Set by the database on insert. `protected set` rather than `init` because EF needs to
    // write it back after SaveChanges, and the seeder occasionally assigns explicit ids.
    public int Id { get; protected set; }

    protected Entity()
    {
    }

    // Identity comparison, not structural: two Employee rows with the same name are still two
    // different people.
    public override bool Equals(object? obj)
    {
        // The reference check is load-bearing, not an optimisation. An entity that has not
        // been saved yet has Id == 0, and without this it would not even equal itself —
        // which quietly breaks List.Remove and Contains on a not-yet-persisted aggregate.
        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj is not Entity other || obj.GetType() != GetType())
        {
            return false;
        }

        return Id != 0 && other.Id != 0 && Id == other.Id;
    }

    public override int GetHashCode() => Id == 0 ? base.GetHashCode() : HashCode.Combine(GetType(), Id);
}
