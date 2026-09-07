using MediatR;
using RequestDesk.Application.Common.Interfaces;
using RequestDesk.Application.Contracts;

namespace RequestDesk.Application.Reports;

public sealed record GetSummaryReportQuery : IRequest<SummaryReport>;

internal sealed class GetSummaryReportQueryHandler(ISummaryReport report) : IRequestHandler<GetSummaryReportQuery, SummaryReport>
{
    public Task<SummaryReport> Handle(GetSummaryReportQuery query, CancellationToken cancellationToken) =>
        report.GetAsync(cancellationToken);
}
