using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RequestDesk.Application.Common.Interfaces;
using RequestDesk.Application.Contracts;
using RequestDesk.Domain.Users;
using RequestDesk.Infrastructure.Persistence;

namespace RequestDesk.Infrastructure.Identity;

/// <summary>
/// Checks passwords through ASP.NET Core Identity and issues a short-lived JWT plus a rotating
/// refresh token. Both failure paths return null so the API can answer with the same vague 401.
/// </summary>
internal sealed class IdentityAuthenticator(
    UserManager<ApplicationUser> userManager,
    AppDbContext db,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider clock) : IAuthenticator
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<AuthResult?> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var credential = await userManager.FindByEmailAsync(email.Trim());

        if (credential is null || !await userManager.CheckPasswordAsync(credential, password))
        {
            return null;
        }

        var profile = await db.Profiles.AsNoTracking().FirstOrDefaultAsync(u => u.Id == credential.Id, cancellationToken);

        if (profile is null || !profile.IsActive)
        {
            return null;
        }

        return await IssueAsync(profile, cancellationToken);
    }

    public async Task<AuthResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var hash = Hash(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (stored is null)
        {
            return null;
        }

        if (!stored.IsUsable(now))
        {
            // A token presented after it was rotated is either a replay or a stolen copy. Either way
            // the whole session is untrustworthy, so every live token for the user is revoked.
            if (stored.RevokedAt is not null)
            {
                await db.RefreshTokens
                    .Where(t => t.UserId == stored.UserId && t.RevokedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
            }

            return null;
        }

        var profile = await db.Profiles.AsNoTracking().FirstOrDefaultAsync(u => u.Id == stored.UserId, cancellationToken);

        if (profile is null || !profile.IsActive)
        {
            return null;
        }

        var result = await IssueAsync(profile, cancellationToken, stored);
        return result;
    }

    private async Task<AuthResult> IssueAsync(AppUser profile, CancellationToken cancellationToken, RefreshToken? replacing = null)
    {
        var now = clock.GetUtcNow();
        var accessExpires = now.AddMinutes(_jwt.AccessTokenMinutes);
        var refreshExpires = now.AddDays(_jwt.RefreshTokenDays);

        var rawRefresh = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var refresh = new RefreshToken
        {
            UserId = profile.Id,
            TokenHash = Hash(rawRefresh),
            CreatedAt = now,
            ExpiresAt = refreshExpires,
        };

        if (replacing is not null)
        {
            replacing.RevokedAt = now;
            replacing.ReplacedByTokenHash = refresh.TokenHash;
        }

        db.RefreshTokens.Add(refresh);
        await db.SaveChangesAsync(cancellationToken);

        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = profile.Id.ToString(),
            [JwtRegisteredClaimNames.Email] = profile.Email,
            [Jwt.NameClaim] = profile.DisplayName,
            [Jwt.RoleClaim] = profile.Role.ToString(),
        };

        if (profile.CustomerId is { } customerId)
        {
            claims[Jwt.CustomerIdClaim] = customerId.ToString();
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = accessExpires.UtcDateTime,
            Claims = claims,
            SigningCredentials = new SigningCredentials(Jwt.SigningKey(_jwt), SecurityAlgorithms.HmacSha256),
        };

        var accessToken = new JsonWebTokenHandler().CreateToken(descriptor);

        return new AuthResult(
            accessToken,
            accessExpires,
            rawRefresh,
            refreshExpires,
            new UserSummary(profile.Id, profile.DisplayName, profile.Role),
            profile.CustomerId);
    }

    private static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
