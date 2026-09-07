using System.Collections.Frozen;

namespace RequestDesk.Application.Requests;

/// <summary>
/// What may be uploaded. A short allowlist of content types and a hard size cap, checked before
/// a single byte is written to storage. The front end mirrors these in <c>web/src/app/core/limits.ts</c>.
/// </summary>
public static class AttachmentRules
{
    public const long MaxSizeBytes = 10 * 1024 * 1024;

    public static readonly FrozenSet<string> AllowedContentTypes = new[]
    {
        "image/png",
        "image/jpeg",
        "image/gif",
        "image/webp",
        "application/pdf",
        "text/plain",
        "text/csv",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static bool IsAllowedContentType(string? contentType) =>
        contentType is not null && AllowedContentTypes.Contains(contentType.Split(';')[0].Trim());
}
