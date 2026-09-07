using RequestDesk.Domain.Users;

namespace RequestDesk.Application.Common.Interfaces;

/// <summary>The authenticated caller, built from JWT claims by the API layer.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Throws if there is no authenticated user. Handlers behind an authorize attribute can call this freely.</summary>
    Actor Actor { get; }
}
