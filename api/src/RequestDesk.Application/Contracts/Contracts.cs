using RequestDesk.Domain.Requests;
using RequestDesk.Domain.Users;

namespace RequestDesk.Application.Contracts;

public sealed record UserSummary(Guid Id, string DisplayName, UserRole Role);

public sealed record CustomerSummary(Guid Id, string Name, string? Organization);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record RequestListItem(
    Guid Id,
    string ReferenceNumber,
    string Title,
    RequestPriority Priority,
    RequestStatus Status,
    Guid CustomerId,
    string CustomerName,
    Guid? AssignedAgentId,
    string? AssignedAgentName,
    int CommentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>What the caller may do to this request right now. The UI renders exactly this and guesses at nothing.</summary>
public sealed record RequestPermissions(bool CanComment, bool CanAttach, bool CanChangeAssignment);

public sealed record CommentDto(Guid Id, string Body, UserSummary Author, DateTimeOffset CreatedAt);

public sealed record AttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    UserSummary UploadedBy,
    DateTimeOffset CreatedAt);

public sealed record HistoryDto(
    Guid Id,
    RequestStatus? FromStatus,
    RequestStatus ToStatus,
    UserSummary Actor,
    string? Reason,
    DateTimeOffset OccurredAt);

public sealed record RequestDetail(
    Guid Id,
    string ReferenceNumber,
    string Title,
    string Description,
    RequestPriority Priority,
    RequestStatus Status,
    CustomerSummary Customer,
    UserSummary? AssignedAgent,
    UserSummary CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<RequestStatus> AllowedTransitions,
    RequestPermissions Permissions,
    IReadOnlyList<CommentDto> Comments,
    IReadOnlyList<AttachmentDto> Attachments,
    IReadOnlyList<HistoryDto> History);

public sealed record StatusChangeResult(
    Guid Id,
    RequestStatus Status,
    HistoryDto Entry,
    IReadOnlyList<RequestStatus> AllowedTransitions,
    DateTimeOffset UpdatedAt);

public sealed record AttachmentContent(Stream Content, string FileName, string ContentType, long SizeBytes);

public sealed record AuthResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserSummary User,
    Guid? CustomerId);

public sealed record StatusCount(RequestStatus Status, int Count);

public sealed record PriorityCount(RequestPriority Priority, int Count);

/// <summary>How long open requests have been waiting, bucketed by age.</summary>
public sealed record AgingBucket(string Label, int MinDays, int? MaxDays, int Count);

public sealed record SummaryReport(
    int OpenCount,
    int InProgressCount,
    int BlockedCount,
    int ResolvedThisWeek,
    int CreatedThisWeek,
    double? MedianOpenAgeHours,
    IReadOnlyList<StatusCount> ByStatus,
    IReadOnlyList<PriorityCount> OpenByPriority,
    IReadOnlyList<AgingBucket> Aging,
    DateTimeOffset GeneratedAt);
