namespace RequestDesk.Infrastructure.Identity;

/// <summary>
/// A refresh token as stored: only its SHA-256 hash is kept, so a copy of the database cannot be
/// used to mint sessions. Tokens rotate on every use and the replaced one is marked, which is
/// what lets a reuse be detected.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid UserId { get; init; }

    public string TokenHash { get; init; } = null!;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? ReplacedByTokenHash { get; set; }

    public bool IsUsable(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
