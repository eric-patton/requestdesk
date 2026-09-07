using RequestDesk.Application.Contracts;
using RequestDesk.Domain.Requests;
using RequestDesk.Domain.Users;

namespace RequestDesk.Application.Requests;

internal static class RequestMapper
{
    /// <summary>Every user id the detail view needs a name for, so they can be looked up in one query.</summary>
    public static IEnumerable<Guid> UserIdsIn(ServiceRequest request)
    {
        yield return request.CreatedById;

        if (request.AssignedAgentId is { } agentId)
        {
            yield return agentId;
        }

        foreach (var comment in request.Comments)
        {
            yield return comment.AuthorId;
        }

        foreach (var attachment in request.Attachments)
        {
            yield return attachment.UploadedById;
        }

        foreach (var entry in request.History)
        {
            yield return entry.ActorId;
        }
    }

    public static RequestDetail ToDetail(ServiceRequest request, Actor actor, IReadOnlyDictionary<Guid, UserSummary> users)
    {
        var customer = request.Customer ?? throw new InvalidOperationException("The customer must be loaded with the request.");

        return new RequestDetail(
            request.Id,
            request.ReferenceNumber,
            request.Title,
            request.Description,
            request.Priority,
            request.Status,
            new CustomerSummary(customer.Id, customer.Name, customer.Organization),
            request.AssignedAgentId is { } agentId ? Resolve(users, agentId) : null,
            Resolve(users, request.CreatedById),
            request.CreatedAt,
            request.UpdatedAt,
            RequestPolicy.AllowedTransitions(request, actor),
            new RequestPermissions(
                RequestPolicy.CanComment(request, actor),
                RequestPolicy.CanAttach(request, actor),
                RequestPolicy.CanChangeAssignment(request, actor)),
            request.Comments.OrderBy(c => c.CreatedAt).Select(c => ToComment(c, users)).ToArray(),
            request.Attachments.OrderBy(a => a.CreatedAt).Select(a => ToAttachment(a, users)).ToArray(),
            request.History.OrderBy(h => h.OccurredAt).Select(h => ToHistory(h, users)).ToArray());
    }

    public static CommentDto ToComment(RequestComment comment, IReadOnlyDictionary<Guid, UserSummary> users) =>
        new(comment.Id, comment.Body, Resolve(users, comment.AuthorId), comment.CreatedAt);

    public static AttachmentDto ToAttachment(RequestAttachment attachment, IReadOnlyDictionary<Guid, UserSummary> users) =>
        new(attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeBytes, Resolve(users, attachment.UploadedById), attachment.CreatedAt);

    public static HistoryDto ToHistory(RequestStatusHistory entry, IReadOnlyDictionary<Guid, UserSummary> users) =>
        new(entry.Id, entry.FromStatus, entry.ToStatus, Resolve(users, entry.ActorId), entry.Reason, entry.OccurredAt);

    /// <summary>A deactivated or deleted user still has to render in a timeline, so an unknown id gets a placeholder rather than an error.</summary>
    private static UserSummary Resolve(IReadOnlyDictionary<Guid, UserSummary> users, Guid id) =>
        users.TryGetValue(id, out var user) ? user : new UserSummary(id, "Former user", UserRole.Agent);
}
