using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Genelife.Api.Auth;

public sealed record IssuedToken(string Token, DateTimeOffset ExpiresAt);

public sealed class JwtTokenService
{
    private readonly JwtOptions options;
    private readonly JwtSigningKeyProvider keyProvider;

    public JwtTokenService(IOptions<JwtOptions> options, JwtSigningKeyProvider keyProvider)
    {
        this.options = options.Value;
        this.keyProvider = keyProvider;
    }

    public IssuedToken CreateToken(string playerId, bool isAdmin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);

        var now = DateTimeOffset.UtcNow;
        var lifetimeMinutes = options.TokenLifetimeMinutes > 0 ? options.TokenLifetimeMinutes : 60;
        var expiresAt = now.AddMinutes(lifetimeMinutes);

        var claims = new List<Claim>
        {
            new("sub", playerId),
            new(AuthClaimTypes.PlayerId, playerId),
            new("jti", Guid.NewGuid().ToString("N")),
            new(AuthClaimTypes.Role, AuthRoles.Player)
        };

        if (isAdmin)
            claims.Add(new Claim(AuthClaimTypes.Role, AuthRoles.Admin));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(keyProvider.Key, SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new IssuedToken(token, expiresAt);
    }
}
