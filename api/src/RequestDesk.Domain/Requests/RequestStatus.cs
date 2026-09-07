namespace RequestDesk.Domain.Requests;

/// <summary>
/// The lifecycle of a service request. Which moves are legal is defined in one place,
/// <see cref="RequestStatusMachine"/>, and nowhere else.
/// </summary>
public enum RequestStatus
{
    New = 1,
    Triaged = 2,
    InProgress = 3,
    Blocked = 4,
    Resolved = 5,
    Closed = 6,
    Cancelled = 7,
}
