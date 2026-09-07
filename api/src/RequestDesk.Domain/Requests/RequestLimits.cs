namespace RequestDesk.Domain.Requests;

/// <summary>
/// Field limits shared by the domain, the request validators and the front end.
/// The Angular form mirrors these numbers in <c>web/src/app/core/limits.ts</c>.
/// </summary>
public static class RequestLimits
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 4000;
    public const int MaxCommentLength = 4000;
    public const int MaxReasonLength = 500;
    public const int MaxFileNameLength = 255;
}
