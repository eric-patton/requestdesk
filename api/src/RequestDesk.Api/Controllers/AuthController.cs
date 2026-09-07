using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RequestDesk.Api.Models;
using RequestDesk.Application.Auth;
using RequestDesk.Application.Contracts;

namespace RequestDesk.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
[Produces("application/json")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    /// <summary>Sign in with an email and password. Returns a short-lived access token and a rotating refresh token.</summary>
    [HttpPost("login")]
    [ProducesResponseType<AuthResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<AuthResult> Login([FromBody] LoginBody body, CancellationToken cancellationToken) =>
        sender.Send(new LoginCommand(body.Email, body.Password), cancellationToken);

    /// <summary>Exchange a refresh token for a new pair. The presented token is revoked; presenting it again revokes the whole session.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType<AuthResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<AuthResult> Refresh([FromBody] RefreshBody body, CancellationToken cancellationToken) =>
        sender.Send(new RefreshTokenCommand(body.RefreshToken), cancellationToken);
}

internal static class RateLimitPolicies
{
    public const string Auth = "auth";
}
