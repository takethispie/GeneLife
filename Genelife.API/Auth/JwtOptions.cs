namespace Genelife.Api.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Auth:Jwt";

    public string Issuer { get; set; } = "genelife";

    public string Audience { get; set; } = "genelife";

    /// <summary>
    /// HMAC-SHA256 signing key. Must be at least 32 bytes for production use.
    /// When unset, an ephemeral key is generated for the lifetime of the process.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Shared secret required to request an Admin token from the development
    /// token endpoint. When unset, Admin tokens cannot be issued over HTTP.
    /// </summary>
    public string? AdminSecret { get; set; }

    public int TokenLifetimeMinutes { get; set; } = 60;
}
