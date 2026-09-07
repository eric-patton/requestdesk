using Microsoft.EntityFrameworkCore;
using RequestDesk.Application.Common;
using RequestDesk.Application.Common.Interfaces;
using RequestDesk.Domain.Requests;

namespace RequestDesk.Infrastructure.Persistence.Repositories;

internal sealed class ServiceRequestRepository(AppDbContext db) : IServiceRequestRepository
{
    public Task<ServiceRequest?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Requests
            .Include(r => r.Customer)
            .Include(r => r.Comments)
            .Include(r => r.Attachments)
            .Include(r => r.History)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public void Add(ServiceRequest request) => db.Requests.Add(request);
}

internal sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException();
        }
    }
}
