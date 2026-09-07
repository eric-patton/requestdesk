using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RequestDesk.Api.Models;
using RequestDesk.Application.Contracts;
using RequestDesk.Application.Requests;
using RequestDesk.Application.Requests.Commands;
using RequestDesk.Application.Requests.Queries;

namespace RequestDesk.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/requests")]
[Produces("application/json")]
public sealed class RequestsController(ISender sender) : ControllerBase
{
    /// <summary>A page of requests. Customers see only their own account's requests; staff see everything.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<RequestListItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<RequestListItem>> List([FromQuery] RequestListParameters parameters, CancellationToken cancellationToken) =>
        sender.Send(
            new ListRequestsQuery(
                parameters.Page,
                parameters.PageSize,
                parameters.Status,
                parameters.Priority,
                parameters.AssignedAgentId,
                parameters.Unassigned,
                parameters.Search,
                parameters.SortBy,
                parameters.SortDescending),
            cancellationToken);

    /// <summary>One request with its comments, attachments, full status history, and what the caller may do next.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<RequestDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<RequestDetail> Get(Guid id, CancellationToken cancellationToken) =>
        sender.Send(new GetRequestQuery(id), cancellationToken);

    /// <summary>Open a request. Staff must name the customer; a customer's request lands on their own account.</summary>
    [HttpPost]
    [ProducesResponseType<RequestDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RequestDetail>> Create([FromBody] CreateRequestBody body, CancellationToken cancellationToken)
    {
        var detail = await sender.Send(new CreateRequestCommand(body.Title, body.Description, body.Priority, body.CustomerId), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = detail.Id }, detail);
    }

    /// <summary>
    /// Move the request to a new status. An illegal move returns 409 with the legal targets in the
    /// body; a legal move this role may not make returns 403. Every success appends one history row.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType<StatusChangeResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<StatusChangeResult> ChangeStatus(Guid id, [FromBody] ChangeStatusBody body, CancellationToken cancellationToken) =>
        sender.Send(new ChangeStatusCommand(id, body.To, body.Reason), cancellationToken);

    /// <summary>Assign, reassign or unassign. Agents may claim an unassigned request; only admins may reassign or clear one.</summary>
    [HttpPatch("{id:guid}/assignment")]
    [Authorize(Roles = "Admin,Agent")]
    [ProducesResponseType<RequestDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<RequestDetail> Assign(Guid id, [FromBody] AssignBody body, CancellationToken cancellationToken) =>
        sender.Send(new AssignRequestCommand(id, body.AgentId), cancellationToken);

    /// <summary>Add a comment. Anyone who can see the request can comment, including on closed requests.</summary>
    [HttpPost("{id:guid}/comments")]
    [ProducesResponseType<CommentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommentDto>> AddComment(Guid id, [FromBody] AddCommentBody body, CancellationToken cancellationToken)
    {
        var comment = await sender.Send(new AddCommentCommand(id, body.Body), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, comment);
    }

    /// <summary>
    /// Upload one file as multipart form data under the field name <c>file</c>. Capped at 10 MB and
    /// limited to a short allowlist of content types. Only open requests accept attachments.
    /// </summary>
    [HttpPost("{id:guid}/attachments")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(AttachmentRules.MaxSizeBytes + 1024 * 1024)]
    [ProducesResponseType<AttachmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AttachmentDto>> AddAttachment(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();

        var attachment = await sender.Send(
            new AddAttachmentCommand(id, Path.GetFileName(file.FileName), file.ContentType, file.Length, content),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, attachment);
    }

    /// <summary>Download an attachment. Served through the API with the stored content type, never from a static path.</summary>
    [HttpGet("{id:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAttachment(Guid id, Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await sender.Send(new DownloadAttachmentQuery(id, attachmentId), cancellationToken);
        return File(attachment.Content, attachment.ContentType, attachment.FileName);
    }
}
