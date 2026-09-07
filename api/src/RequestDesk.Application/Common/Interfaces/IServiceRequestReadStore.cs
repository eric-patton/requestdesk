using RequestDesk.Application.Contracts;
using RequestDesk.Application.Requests.Queries;
using RequestDesk.Domain.Users;

namespace RequestDesk.Application.Common.Interfaces;

/// <summary>
/// Read-side queries that need paging, filtering and sorting composed in the database. Kept apart
/// from the repository so the write side never exposes an <c>IQueryable</c>.
/// </summary>
public interface IServiceRequestReadStore
{
    /// <summary>Pages requests on the server. Customers are scoped to their own account before any filter is applied.</summary>
    Task<PagedResult<RequestListItem>> ListAsync(ListRequestsQuery query, Actor actor, CancellationToken cancellationToken);
}
