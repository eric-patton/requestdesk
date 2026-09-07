using System.Security.Claims;
using RequestDesk.Application.Common.Interfaces;
using RequestDesk.Domain.Users;
using RequestDesk.Infrastructure.Identity;

namespace RequestDesk.Api.Auth;

/// <summary>Turns the validated JWT into the domain's <see cref="Actor"/>. No database access.</summary>
internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private Actor? _actor;

    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Actor Actor => _actor ??= Build();

    private Actor Build()
    {
        var principal = accessor.HttpContext?.User;

        if (principal?.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException("There is no authenticated user on this request.");
        }

        var userId = Guid.Parse(principal.FindFirstValue("sub") ?? throw new InvalidOperationException("The token has no subject."));
        var role = Enum.Parse<UserRole>(principal.FindFirstValue(Jwt.RoleClaim) ?? throw new InvalidOperationException("The token has no role."));
        var customerId = principal.FindFirstValue(Jwt.CustomerIdClaim) is { } raw ? Guid.Parse(raw) : (Guid?)null;

        return new Actor(userId, role, customerId);
    }
}
