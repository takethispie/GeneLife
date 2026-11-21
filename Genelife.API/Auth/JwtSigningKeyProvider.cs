using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Genelife.Api.Auth;

/// <summary>
/// Resolves the symmetric signing key once so token issuance and validation
/// always use the same key for the lifetime of the process.
/// </summary>
public sealed class JwtSigningKeyProvider
{
    private const int MinimumKeyBytes = 32;

    public JwtSigningKeyProvider(IOptions<JwtOptions> options, ILogger<JwtSigningKeyProvider> logger)
    {
        var configuredKey = options.Value.Key;

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            logger.LogWarning(
                "Auth:Jwt:Key is not configured; using an ephemeral signing key. Tokens will be invalid after restart.");
            Key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(MinimumKeyBytes));
            return;
        }

        var keyBytes = Encoding.UTF8.GetBytes(configuredKey);
        if (keyBytes.Length < MinimumKeyBytes)
        {
            logger.LogWarning(
                "Auth:Jwt:Key is shorter than {MinimumKeyBytes} bytes; using an ephemeral signing key instead.",
                MinimumKeyBytes);
            Key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(MinimumKeyBytes));
            return;
        }

        Key = new SymmetricSecurityKey(keyBytes);
    }

    public SymmetricSecurityKey Key { get; }
}
