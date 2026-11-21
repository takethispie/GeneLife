using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Genelife.Api.Auth;

namespace Genelife.Api.Endpoints;

public static class AuthEndpoints
{
    private const int MaxPlayerIdLength = 128;

    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/token", (
            TokenRequest request,
            JwtTokenService tokenService,
            IOptions<JwtOptions> jwtOptions) =>
        {
            if (string.IsNullOrWhiteSpace(request.PlayerId) || request.PlayerId.Trim().Length > MaxPlayerIdLength)
                return Results.BadRequest(new { message = $"playerId is required (max {MaxPlayerIdLength} characters)" });

            var playerId = request.PlayerId.Trim();
            var isAdmin = false;

            if (!string.IsNullOrWhiteSpace(request.AdminSecret))
            {
                var expectedSecret = jwtOptions.Value.AdminSecret;
                if (string.IsNullOrEmpty(expectedSecret) || !SecretsMatch(request.AdminSecret, expectedSecret))
                {
                    return Results.Json(
                        new { error = "forbidden", message = "Invalid admin secret." },
                        statusCode: StatusCodes.Status403Forbidden);
                }

                isAdmin = true;
            }

            var issued = tokenService.CreateToken(playerId, isAdmin);
            var roles = isAdmin
                ? new[] { AuthRoles.Player, AuthRoles.Admin }
                : new[] { AuthRoles.Player };

            return Results.Ok(new
            {
                accessToken = issued.Token,
                tokenType = "Bearer",
                expiresAt = issued.ExpiresAt,
                playerId,
                roles
            });
        })
        .AllowAnonymous()
        .WithName("IssueToken");
    }

    private static bool SecretsMatch(string provided, string expected)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);

        return providedBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }

    public sealed record TokenRequest(string? PlayerId, string? AdminSecret);
}
