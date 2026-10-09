namespace IntelliQueryAI.SharedKernel.Primitives;

/// <summary>
/// Base class for all domain entities having a strongly typed identifier.
/// </summary>
/// <typeparam name="TId">The type of the entity identifier.</typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    /// <summary>
    /// Gets or sets the entity unique identifier.
    /// </summary>
    public TId Id { get; protected set; } = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="Entity{TId}"/> class.
    /// </summary>
    protected Entity(TId id)
    {
        Id = id;
    }

    /// <summary>
    /// Required for EF Core deserialization.
    /// </summary>
    protected Entity()
    {
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return obj is Entity<TId> entity && EqualityComparer<TId>.Default.Equals(Id, entity.Id);
    }

    /// <inheritdoc />
    public bool Equals(Entity<TId>? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (other.GetType() != GetType()) return false;
        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return EqualityComparer<TId>.Default.GetHashCode(Id);
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
    {
        return Equals(left, right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
    {
        return !Equals(left, right);
    }
}

/// <summary>
/// Base entity using <see cref="Guid"/> as default primary key identifier.
/// </summary>
public abstract class Entity : Entity<Guid>
{
    /// <summary>
    /// Initializes a new instance with a specific <see cref="Guid"/> identifier.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    protected Entity(Guid id) : base(id)
    {
    }

    /// <summary>
    /// Initializes a new instance with a newly generated <see cref="Guid"/>.
    /// </summary>
    protected Entity() : base(Guid.NewGuid())
    {
    }
}
