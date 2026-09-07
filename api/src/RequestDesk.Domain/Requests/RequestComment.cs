using RequestDesk.Domain.Common;

namespace RequestDesk.Domain.Requests;

public sealed class RequestComment : Entity
{
    private RequestComment()
    {
    }

    public Guid RequestId { get; private set; }

    public string Body { get; private set; } = null!;

    public Guid AuthorId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static RequestComment Create(Guid requestId, string body, Guid authorId, DateTimeOffset now)
    {
        body = (body ?? string.Empty).Trim();

        if (body.Length == 0)
        {
            throw new DomainRuleException("A comment needs some text.");
        }

        if (body.Length > RequestLimits.MaxCommentLength)
        {
            throw new DomainRuleException($"A comment may be at most {RequestLimits.MaxCommentLength} characters.");
        }

        return new RequestComment
        {
            RequestId = requestId,
            Body = body,
            AuthorId = authorId,
            CreatedAt = now,
        };
    }
}
