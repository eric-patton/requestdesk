using System.Collections.Frozen;

namespace RequestDesk.Domain.Requests;

/// <summary>
/// Every legal status transition, in one place, with no framework involved.
/// </summary>
/// <remarks>
/// <code>
/// New         ->  Triaged | Cancelled
/// Triaged     ->  InProgress | Cancelled
/// InProgress  ->  Blocked | Resolved
/// Blocked     ->  InProgress | Cancelled
/// Resolved    ->  Closed | InProgress    (reopen)
/// Closed      ->  terminal
/// Cancelled   ->  terminal
/// </code>
/// Targets are listed in enum order so the API always returns them in a stable order.
/// This class says what is legal. Who is allowed to make a legal move is <see cref="RequestPolicy"/>.
/// </remarks>
public static class RequestStatusMachine
{
    private static readonly FrozenDictionary<RequestStatus, RequestStatus[]> Transitions =
        new Dictionary<RequestStatus, RequestStatus[]>
        {
            [RequestStatus.New] = [RequestStatus.Triaged, RequestStatus.Cancelled],
            [RequestStatus.Triaged] = [RequestStatus.InProgress, RequestStatus.Cancelled],
            [RequestStatus.InProgress] = [RequestStatus.Blocked, RequestStatus.Resolved],
            [RequestStatus.Blocked] = [RequestStatus.InProgress, RequestStatus.Cancelled],
            [RequestStatus.Resolved] = [RequestStatus.InProgress, RequestStatus.Closed],
            [RequestStatus.Closed] = [],
            [RequestStatus.Cancelled] = [],
        }.ToFrozenDictionary();

    /// <summary>Every status, in enum order.</summary>
    public static IReadOnlyList<RequestStatus> All { get; } = Enum.GetValues<RequestStatus>();

    /// <summary>The statuses a request in <paramref name="from"/> may legally move to. Empty for terminal states.</summary>
    public static IReadOnlyList<RequestStatus> LegalTargets(RequestStatus from) => Transitions[from];

    public static bool CanTransition(RequestStatus from, RequestStatus to) => Transitions[from].Contains(to);

    /// <summary>Closed and Cancelled. Nothing moves out of a terminal state.</summary>
    public static bool IsTerminal(RequestStatus status) => Transitions[status].Length == 0;

    public static bool IsOpen(RequestStatus status) => !IsTerminal(status);

    /// <summary>The one move that reverses a resolution. Admin only, see <see cref="RequestPolicy"/>.</summary>
    public static bool IsReopen(RequestStatus from, RequestStatus to) =>
        from == RequestStatus.Resolved && to == RequestStatus.InProgress;
}
