using RequestDesk.Application.Contracts;
using RequestDesk.Domain.Users;

namespace RequestDesk.Application.Common.Interfaces;

/// <summary>Read access to user profiles: names for timelines and the staff list for the assignment control.</summary>
public interface IUserDirectory
{
    Task<AppUser?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Admins and agents who can be assigned work.</summary>
    Task<IReadOnlyList<UserSummary>> ListStaffAsync(CancellationToken cancellationToken);

    /// <summary>Resolve a set of ids to display names in one query. Unknown ids are simply absent.</summary>
    Task<IReadOnlyDictionary<Guid, UserSummary>> GetSummariesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken);
}
