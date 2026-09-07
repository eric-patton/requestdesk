namespace RequestDesk.Domain.Users;

public enum UserRole
{
    /// <summary>Watches the queue. May do everything, including reassigning and reopening.</summary>
    Admin = 1,

    /// <summary>Works requests. May do anything except reopen a resolved request or reassign someone else's.</summary>
    Agent = 2,

    /// <summary>Submits requests. May create, comment on and cancel requests belonging to their own customer account.</summary>
    Customer = 3,
}
