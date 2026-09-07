using RequestDesk.Application.Contracts;

namespace RequestDesk.Application.Common.Interfaces;

/// <summary>Credential checking and token issuance. Implemented over ASP.NET Core Identity and JWT bearer tokens.</summary>
public interface IAuthenticator
{
    /// <summary>Returns tokens for a valid email and password, or null when either is wrong.</summary>
    Task<AuthResult?> LoginAsync(string email, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Rotates a refresh token: the presented token is revoked and a new pair is issued. Returns
    /// null when the token is unknown, expired, or has already been used.
    /// </summary>
    Task<AuthResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
}
