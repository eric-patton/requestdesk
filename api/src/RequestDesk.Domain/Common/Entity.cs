namespace RequestDesk.Domain.Common;

/// <summary>
/// Base type for anything with an identity. Ids are version 7 GUIDs so they sort by creation time,
/// which keeps the primary key index in PostgreSQL appending rather than scattering.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();
}
