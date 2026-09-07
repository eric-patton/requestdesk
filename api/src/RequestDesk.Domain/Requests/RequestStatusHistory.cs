namespace RequestDesk.Domain.Requests;

/// <summary>
/// One status change. Append only: this type has no mutators, the persistence layer refuses to
/// update or delete rows, and the database has a trigger that does the same. See
/// <c>docs/decisions/0001-append-only-status-history.md</c>.
/// </summary>
public sealed class RequestStatusHistory
{
    private RequestStatusHistory()
    {
    }

    public Guid Id { get; private init; }

    public Guid RequestId { get; private init; }

    /// <summary>Null for the row written when the request is created.</summary>
    public RequestStatus? FromStatus { get; private init; }

    public RequestStatus ToStatus { get; private init; }

    public Guid ActorId { get; private init; }

    public string? Reason { get; private init; }

    public DateTimeOffset OccurredAt { get; private init; }

    internal static RequestStatusHistory Record(
        Guid requestId,
        RequestStatus? from,
        RequestStatus to,
        Guid actorId,
        string? reason,
        DateTimeOffset occurredAt) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            RequestId = requestId,
            FromStatus = from,
            ToStatus = to,
            ActorId = actorId,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            OccurredAt = occurredAt,
        };
}
