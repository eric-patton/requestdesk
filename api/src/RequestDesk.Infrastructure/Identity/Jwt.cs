using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace RequestDesk.Infrastructure.Identity;

/// <summary>The pieces of the token contract the API layer needs to validate and read tokens.</summary>
public static class Jwt
{
    /// <summary>Claim carrying the customer account id for customer users. Absent for staff.</summary>
    public const string CustomerIdClaim = "customer_id";

    public const string RoleClaim = "role";

    public const string NameClaim = "name";

    public static SymmetricSecurityKey SigningKey(JwtOptions options) =>
        new(Encoding.UTF8.GetBytes(options.SigningKey));
}
