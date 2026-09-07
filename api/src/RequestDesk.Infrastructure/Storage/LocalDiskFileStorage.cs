using Microsoft.Extensions.Options;
using RequestDesk.Application.Common.Interfaces;

namespace RequestDesk.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Directory for attachment bytes. Outside the web root; nothing in it is ever served as a static file.</summary>
    public string Root { get; set; } = "var/attachments";
}

/// <summary>
/// Attachments on local disk under a random key. This is the right answer for docker compose and
/// a single-node demo; the interface is shaped so an S3-compatible implementation can replace it
/// without touching the application layer.
/// </summary>
internal sealed class LocalDiskFileStorage(IOptions<StorageOptions> options, TimeProvider clock) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.Root);

    public async Task<string> SaveAsync(Stream content, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var key = $"{now:yyyy}/{now:MM}/{Guid.NewGuid():N}";
        var path = PathFor(key);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, cancellationToken);

        return key;
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathFor(key);

        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;

        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathFor(key);

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <summary>Resolve a key to a path and refuse anything that would escape the root.</summary>
    private string PathFor(string key)
    {
        var path = Path.GetFullPath(Path.Combine(_root, key));

        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Storage key resolves outside the storage root.");
        }

        return path;
    }
}
