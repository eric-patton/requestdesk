using Microsoft.EntityFrameworkCore;
using RequestDesk.Application.Common.Interfaces;
using RequestDesk.Application.Contracts;
using RequestDesk.Domain.Users;

namespace RequestDesk.Infrastructure.Persistence.Repositories;

internal sealed class UserDirectory(AppDbContext db) : IUserDirectory
{
    public Task<AppUser?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Profiles.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<IReadOnlyList<UserSummary>> ListStaffAsync(CancellationToken cancellationToken) =>
        await db.Profiles
            .AsNoTracking()
            .Where(u => u.IsActive && (u.Role == UserRole.Admin || u.Role == UserRole.Agent))
            .OrderBy(u => u.DisplayName)
            .Select(u => new UserSummary(u.Id, u.DisplayName, u.Role))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, UserSummary>> GetSummariesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.Distinct().ToArray();

        if (wanted.Length == 0)
        {
            return new Dictionary<Guid, UserSummary>();
        }

        return await db.Profiles
            .AsNoTracking()
            .Where(u => wanted.Contains(u.Id))
            .Select(u => new UserSummary(u.Id, u.DisplayName, u.Role))
            .ToDictionaryAsync(u => u.Id, cancellationToken);
    }
}
