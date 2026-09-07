using RequestDesk.Domain.Requests;

namespace RequestDesk.Application.Common.Interfaces;

/// <summary>Write-side access to the request aggregate. Loads the whole aggregate, tracked, ready to mutate.</summary>
public interface IServiceRequestRepository
{
    Task<ServiceRequest?> GetAsync(Guid id, CancellationToken cancellationToken);

    void Add(ServiceRequest request);
}

/// <summary>Commits whatever the repositories have changed in one transaction.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
