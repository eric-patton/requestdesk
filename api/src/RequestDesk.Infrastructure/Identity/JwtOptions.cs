namespace RequestDesk.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 needs at least 32 bytes. Startup refuses anything shorter.</summary>
    public const int MinimumSigningKeyLength = 32;

    public string Issuer { get; set; } = "requestdesk";

    public string Audience { get; set; } = "requestdesk";

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 7;
}
