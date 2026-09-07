using RequestDesk.Domain.Requests;

namespace RequestDesk.Api.Models;

/// <summary>Request bodies. Kept separate from the application commands so the HTTP shape can change without touching handlers.</summary>
public sealed record LoginBody(string Email, string Password);

public sealed record RefreshBody(string RefreshToken);

public sealed record CreateRequestBody(string Title, string Description, RequestPriority Priority, Guid? CustomerId);

public sealed record ChangeStatusBody(RequestStatus To, string? Reason);

/// <param name="AgentId">The agent to assign, or null to unassign.</param>
public sealed record AssignBody(Guid? AgentId);

public sealed record AddCommentBody(string Body);

/// <summary>Query string for the request list. Everything is optional.</summary>
public sealed class RequestListParameters
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    /// <summary>Repeat the parameter to include several statuses: <c>?status=New&amp;status=Triaged</c>.</summary>
    public RequestStatus[]? Status { get; set; }

    public RequestPriority[]? Priority { get; set; }

    public Guid? AssignedAgentId { get; set; }

    public bool Unassigned { get; set; }

    /// <summary>Matches the title, the reference number and the customer name, case-insensitively.</summary>
    public string? Search { get; set; }

    /// <summary>One of updatedAt, createdAt, priority, status, title, referenceNumber, customer.</summary>
    public string SortBy { get; set; } = "updatedAt";

    public bool SortDescending { get; set; } = true;
}
