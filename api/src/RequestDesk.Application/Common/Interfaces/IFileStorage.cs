namespace RequestDesk.Application.Common.Interfaces;

/// <summary>
/// Where attachment bytes live. Keys are opaque and random; nothing about them is derived from the
/// file name. The local-disk implementation writes outside the web root, and an S3-compatible one
/// is the intended swap for a real deployment.
/// </summary>
public interface IFileStorage
{
    /// <summary>Store the content and return the key it can be read back with.</summary>
    Task<string> SaveAsync(Stream content, CancellationToken cancellationToken);

    /// <summary>Open the content for reading, or null if the key is unknown.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
