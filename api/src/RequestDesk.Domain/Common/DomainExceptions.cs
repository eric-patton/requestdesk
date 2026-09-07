using RequestDesk.Domain.Requests;

namespace RequestDesk.Domain.Common;

/// <summary>Base for every exception the domain raises. The API maps each subtype to an HTTP status.</summary>
public abstract class DomainException(string message) : Exception(message);

/// <summary>An invariant was violated: a blank title, a comment that is too long, and so on. Maps to 400.</summary>
public sealed class DomainRuleException(string message) : DomainException(message);

/// <summary>The actor is not allowed to do this to this request. Maps to 403.</summary>
public sealed class PermissionDeniedException(string message) : DomainException(message);

/// <summary>
/// The requested status change is not a legal move in the state machine, regardless of who asked.
/// Carries the legal targets so the API can return them in the 409 body.
/// </summary>
public sealed class IllegalTransitionException(
    RequestStatus from,
    RequestStatus to,
    IReadOnlyList<RequestStatus> legalTargets)
    : DomainException($"A request cannot move from {from} to {to}.")
{
    public RequestStatus From { get; } = from;

    public RequestStatus To { get; } = to;

    public IReadOnlyList<RequestStatus> LegalTargets { get; } = legalTargets;
}
