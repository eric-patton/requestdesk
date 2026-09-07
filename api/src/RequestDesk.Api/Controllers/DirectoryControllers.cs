using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RequestDesk.Application.Contracts;
using RequestDesk.Application.Directory;
using RequestDesk.Application.Reports;

namespace RequestDesk.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Agent")]
[Route("api/staff")]
[Produces("application/json")]
public sealed class StaffController(ISender sender) : ControllerBase
{
    /// <summary>Active admins and agents, for the assignment control.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UserSummary>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<UserSummary>> List(CancellationToken cancellationToken) =>
        sender.Send(new ListStaffQuery(), cancellationToken);
}

[ApiController]
[Authorize(Roles = "Admin,Agent")]
[Route("api/customers")]
[Produces("application/json")]
public sealed class CustomersController(ISender sender) : ControllerBase
{
    /// <summary>Customer accounts, for staff opening a request on a customer's behalf.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CustomerSummary>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<CustomerSummary>> List(CancellationToken cancellationToken) =>
        sender.Send(new ListCustomersQuery(), cancellationToken);
}

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/reports")]
[Produces("application/json")]
public sealed class ReportsController(ISender sender) : ControllerBase
{
    /// <summary>Dashboard numbers: counts by status, open requests by priority, median open age and aging buckets. Computed in SQL.</summary>
    [HttpGet("summary")]
    [ProducesResponseType<SummaryReport>(StatusCodes.Status200OK)]
    public Task<SummaryReport> Summary(CancellationToken cancellationToken) =>
        sender.Send(new GetSummaryReportQuery(), cancellationToken);
}
