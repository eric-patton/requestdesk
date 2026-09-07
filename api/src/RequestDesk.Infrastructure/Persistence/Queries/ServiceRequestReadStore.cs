using Microsoft.EntityFrameworkCore;
using RequestDesk.Application.Common.Interfaces;
using RequestDesk.Application.Contracts;
using RequestDesk.Application.Requests.Queries;
using RequestDesk.Domain.Requests;
using RequestDesk.Domain.Users;

namespace RequestDesk.Infrastructure.Persistence.Queries;

/// <summary>
/// The list query. Scope, filters, search, sort, count and page are all composed into one SQL
/// statement plus one count; the client never receives more than a page.
/// </summary>
internal sealed class ServiceRequestReadStore(AppDbContext db) : IServiceRequestReadStore
{
    public async Task<PagedResult<RequestListItem>> ListAsync(ListRequestsQuery query, Actor actor, CancellationToken cancellationToken)
    {
        IQueryable<ServiceRequest> requests = db.Requests.AsNoTracking();

        // Scope first. A customer's filters can only ever narrow their own requests.
        if (actor.IsCustomer)
        {
            requests = requests.Where(r => r.CustomerId == actor.CustomerId);
        }

        if (query.Statuses is { Count: > 0 } statuses)
        {
            requests = requests.Where(r => statuses.Contains(r.Status));
        }

        if (query.Priorities is { Count: > 0 } priorities)
        {
            requests = requests.Where(r => priorities.Contains(r.Priority));
        }

        if (query.AssignedAgentId is { } agentId)
        {
            requests = requests.Where(r => r.AssignedAgentId == agentId);
        }

        if (query.Unassigned)
        {
            requests = requests.Where(r => r.AssignedAgentId == null);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{EscapeLike(query.Search.Trim())}%";
            requests = requests.Where(r =>
                EF.Functions.ILike(r.Title, pattern, "\\")
                || EF.Functions.ILike(r.ReferenceNumber, pattern, "\\")
                || EF.Functions.ILike(r.Customer!.Name, pattern, "\\"));
        }

        var total = await requests.CountAsync(cancellationToken);

        var items = await Sort(requests, query.SortBy, query.SortDescending)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(r => new RequestListItem(
                r.Id,
                r.ReferenceNumber,
                r.Title,
                r.Priority,
                r.Status,
                r.CustomerId,
                r.Customer!.Name,
                r.AssignedAgentId,
                r.AssignedAgent != null ? r.AssignedAgent.DisplayName : null,
                r.Comments.Count(),
                r.CreatedAt,
                r.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<RequestListItem>(items, query.Page, query.PageSize, total);
    }

    private static IOrderedQueryable<ServiceRequest> Sort(IQueryable<ServiceRequest> requests, string sortBy, bool descending)
    {
        // Every sort ends with the id so paging is stable when the sort key ties.
        IOrderedQueryable<ServiceRequest> ordered = sortBy.ToLowerInvariant() switch
        {
            "createdat" => descending ? requests.OrderByDescending(r => r.CreatedAt) : requests.OrderBy(r => r.CreatedAt),
            "priority" => descending ? requests.OrderByDescending(r => r.Priority) : requests.OrderBy(r => r.Priority),
            "status" => descending ? requests.OrderByDescending(r => r.Status) : requests.OrderBy(r => r.Status),
            "title" => descending ? requests.OrderByDescending(r => r.Title) : requests.OrderBy(r => r.Title),
            "referencenumber" => descending ? requests.OrderByDescending(r => r.ReferenceNumber) : requests.OrderBy(r => r.ReferenceNumber),
            "customer" => descending ? requests.OrderByDescending(r => r.Customer!.Name) : requests.OrderBy(r => r.Customer!.Name),
            _ => descending ? requests.OrderByDescending(r => r.UpdatedAt) : requests.OrderBy(r => r.UpdatedAt),
        };

        return ordered.ThenBy(r => r.Id);
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
