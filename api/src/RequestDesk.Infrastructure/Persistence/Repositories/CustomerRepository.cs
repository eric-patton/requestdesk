using Microsoft.EntityFrameworkCore;
using RequestDesk.Application.Common.Interfaces;
using RequestDesk.Application.Contracts;

namespace RequestDesk.Infrastructure.Persistence.Repositories;

internal sealed class CustomerRepository(AppDbContext db) : ICustomerRepository
{
    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        db.Customers.AnyAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CustomerSummary>> ListAsync(CancellationToken cancellationToken) =>
        await db.Customers
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CustomerSummary(c.Id, c.Name, c.Organization))
            .ToListAsync(cancellationToken);
}
