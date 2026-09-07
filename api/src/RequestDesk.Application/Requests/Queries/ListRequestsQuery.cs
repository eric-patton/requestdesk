using FluentValidation;
using MediatR;
using RequestDesk.Application.Common.Interfaces;
using RequestDesk.Application.Contracts;
using RequestDesk.Domain.Requests;

namespace RequestDesk.Application.Requests.Queries;

/// <summary>
/// The request list. Everything here is applied in the database: the page, the filters, the
/// search and the sort. The client never receives more than one page.
/// </summary>
public sealed record ListRequestsQuery(
    int Page = 1,
    int PageSize = 20,
    IReadOnlyList<RequestStatus>? Statuses = null,
    IReadOnlyList<RequestPriority>? Priorities = null,
    Guid? AssignedAgentId = null,
    bool Unassigned = false,
    string? Search = null,
    string SortBy = "updatedAt",
    bool SortDescending = true) : IRequest<PagedResult<RequestListItem>>
{
    public const int MaxPageSize = 100;

    public static readonly IReadOnlyList<string> SortableFields =
    [
        "updatedAt", "createdAt", "priority", "status", "title", "referenceNumber", "customer",
    ];
}

internal sealed class ListRequestsQueryValidator : AbstractValidator<ListRequestsQuery>
{
    public ListRequestsQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, ListRequestsQuery.MaxPageSize);
        RuleFor(q => q.Search).MaximumLength(200);
        RuleFor(q => q.SortBy)
            .Must(field => ListRequestsQuery.SortableFields.Contains(field, StringComparer.OrdinalIgnoreCase))
            .WithMessage(q => $"'{q.SortBy}' is not sortable. Use one of: {string.Join(", ", ListRequestsQuery.SortableFields)}.");
        RuleForEach(q => q.Statuses).IsInEnum();
        RuleForEach(q => q.Priorities).IsInEnum();
    }
}

internal sealed class ListRequestsQueryHandler(IServiceRequestReadStore readStore, ICurrentUser currentUser)
    : IRequestHandler<ListRequestsQuery, PagedResult<RequestListItem>>
{
    public Task<PagedResult<RequestListItem>> Handle(ListRequestsQuery request, CancellationToken cancellationToken) =>
        readStore.ListAsync(request, currentUser.Actor, cancellationToken);
}
