using RequestDesk.Application.Contracts;

namespace RequestDesk.Application.Common.Interfaces;

/// <summary>The dashboard numbers. The one query in the system written in SQL rather than LINQ; see ADR 0002.</summary>
public interface ISummaryReport
{
    Task<SummaryReport> GetAsync(CancellationToken cancellationToken);
}
