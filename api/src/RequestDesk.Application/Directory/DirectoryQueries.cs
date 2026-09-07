using MediatR;
using RequestDesk.Application.Common.Interfaces;
using RequestDesk.Application.Contracts;

namespace RequestDesk.Application.Directory;

/// <summary>Agents and admins, for the assignment control.</summary>
public sealed record ListStaffQuery : IRequest<IReadOnlyList<UserSummary>>;

internal sealed class ListStaffQueryHandler(IUserDirectory users) : IRequestHandler<ListStaffQuery, IReadOnlyList<UserSummary>>
{
    public Task<IReadOnlyList<UserSummary>> Handle(ListStaffQuery query, CancellationToken cancellationToken) =>
        users.ListStaffAsync(cancellationToken);
}

/// <summary>Customer accounts, for staff opening a request on a customer's behalf.</summary>
public sealed record ListCustomersQuery : IRequest<IReadOnlyList<CustomerSummary>>;

internal sealed class ListCustomersQueryHandler(ICustomerRepository customers) : IRequestHandler<ListCustomersQuery, IReadOnlyList<CustomerSummary>>
{
    public Task<IReadOnlyList<CustomerSummary>> Handle(ListCustomersQuery query, CancellationToken cancellationToken) =>
        customers.ListAsync(cancellationToken);
}
