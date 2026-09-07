using RequestDesk.Domain.Users;

namespace RequestDesk.Domain.Requests;

/// <summary>
/// Who may do what to a request. These rules live here, not in the controllers, and the API
/// returns their answers to the front end so the UI never has to guess.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A <b>Customer</b> sees only requests on their own account. They may create, comment on and cancel them.</item>
/// <item>An <b>Agent</b> sees everything and may make any legal move except reopening a resolved request.
/// An agent may claim an unassigned request but may not reassign or unassign one.</item>
/// <item>An <b>Admin</b> may do everything, including reopen and reassign.</item>
/// </list>
/// </remarks>
public static class RequestPolicy
{
    public static bool CanView(ServiceRequest request, Actor actor) =>
        actor.IsStaff || (actor.IsCustomer && actor.CustomerId == request.CustomerId);

    /// <summary>
    /// The legal targets from the request's current status, narrowed to what this actor may do.
    /// Empty when the actor cannot see the request at all.
    /// </summary>
    public static IReadOnlyList<RequestStatus> AllowedTransitions(ServiceRequest request, Actor actor)
    {
        if (!CanView(request, actor))
        {
            return [];
        }

        IReadOnlyList<RequestStatus> legal = RequestStatusMachine.LegalTargets(request.Status);

        return actor.Role switch
        {
            UserRole.Admin => legal,
            UserRole.Agent => legal.Where(to => !RequestStatusMachine.IsReopen(request.Status, to)).ToArray(),
            UserRole.Customer => legal.Where(to => to == RequestStatus.Cancelled).ToArray(),
            _ => [],
        };
    }

    public static bool CanTransition(ServiceRequest request, Actor actor, RequestStatus to) =>
        AllowedTransitions(request, actor).Contains(to);

    public static bool CanComment(ServiceRequest request, Actor actor) => CanView(request, actor);

    /// <summary>Attachments may be added while the request is open. Closed and cancelled requests are frozen.</summary>
    public static bool CanAttach(ServiceRequest request, Actor actor) => CanView(request, actor) && request.IsOpen;

    /// <summary>Whether this actor may change the assignee at all. Drives whether the UI shows the control.</summary>
    public static bool CanChangeAssignment(ServiceRequest request, Actor actor) =>
        request.IsOpen && (actor.IsAdmin || (actor.IsAgent && request.AssignedAgentId is null));

    /// <summary>Whether this actor may set the assignee to this specific value.</summary>
    public static bool CanAssign(ServiceRequest request, Actor actor, Guid? newAgentId)
    {
        if (!request.IsOpen)
        {
            return false;
        }

        return actor.Role switch
        {
            UserRole.Admin => true,
            UserRole.Agent => request.AssignedAgentId is null && newAgentId is not null,
            _ => false,
        };
    }
}
