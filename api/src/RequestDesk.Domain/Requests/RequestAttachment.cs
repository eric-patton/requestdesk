using RequestDesk.Domain.Common;

namespace RequestDesk.Domain.Requests;

/// <summary>
/// Metadata for an uploaded file. The bytes live in file storage under <see cref="StorageKey"/>,
/// which is random and never derived from the file name, so nothing is served from a guessable path.
/// </summary>
public sealed class RequestAttachment : Entity
{
    private RequestAttachment()
    {
    }

    public Guid RequestId { get; private set; }

    public string FileName { get; private set; } = null!;

    public string ContentType { get; private set; } = null!;

    public long SizeBytes { get; private set; }

    public string StorageKey { get; private set; } = null!;

    public Guid UploadedById { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static RequestAttachment Create(
        Guid requestId,
        string fileName,
        string contentType,
        long sizeBytes,
        string storageKey,
        Guid uploadedById,
        DateTimeOffset now)
    {
        fileName = (fileName ?? string.Empty).Trim();
        contentType = (contentType ?? string.Empty).Trim();

        if (fileName.Length == 0 || fileName.Length > RequestLimits.MaxFileNameLength)
        {
            throw new DomainRuleException($"A file name is required and may be at most {RequestLimits.MaxFileNameLength} characters.");
        }

        if (contentType.Length == 0)
        {
            throw new DomainRuleException("A content type is required.");
        }

        if (sizeBytes <= 0)
        {
            throw new DomainRuleException("An attachment cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new DomainRuleException("A storage key is required.");
        }

        return new RequestAttachment
        {
            RequestId = requestId,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            StorageKey = storageKey,
            UploadedById = uploadedById,
            CreatedAt = now,
        };
    }
}
