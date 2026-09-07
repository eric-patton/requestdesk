using RequestDesk.Application.Contracts;

namespace RequestDesk.Application.Common.Interfaces;

public interface ICustomerRepository
{
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<CustomerSummary>> ListAsync(CancellationToken cancellationToken);
}
