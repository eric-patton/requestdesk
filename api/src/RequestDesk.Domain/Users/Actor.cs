namespace RequestDesk.Domain.Users;

/// <summary>
/// Who is performing an action. Built from the authenticated user's claims, so the domain never
/// needs to load a user record to decide what is allowed.
/// </summary>
/// <param name="UserId">The acting user's id.</param>
/// <param name="Role">The acting user's single role.</param>
/// <param name="CustomerId">For customers, the customer account they belong to. Null for staff.</param>
public sealed record Actor(Guid UserId, UserRole Role, Guid? CustomerId)
{
    public bool IsAdmin => Role == UserRole.Admin;

    public bool IsAgent => Role == UserRole.Agent;

    public bool IsCustomer => Role == UserRole.Customer;

    /// <summary>Admins and agents. Staff see every request; customers see only their own.</summary>
    public bool IsStaff => Role is UserRole.Admin or UserRole.Agent;
}
