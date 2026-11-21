using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Genelife.Web.Models;

namespace Genelife.Web.Services;

public sealed class GameDataService
{
    private readonly HttpClient http;
    private readonly string playerId;
    private string? accessToken;
    private DateTimeOffset tokenExpiresAt;

    public GameDataService(HttpClient http, IConfiguration configuration)
    {
        this.http = http;

        var baseUrl = configuration["ApiBaseUrl"];
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        }

        http.Timeout = TimeSpan.FromSeconds(10);

        playerId = configuration["AdminPlayerId"] ?? "admin-ui";

        ApiBaseUrl = http.BaseAddress?.ToString().TrimEnd('/') ?? string.Empty;
    }

    public string ApiBaseUrl { get; }

    public async Task<WorldData> GetWorldDataAsync(CancellationToken cancellationToken = default)
    {
        await EnsureTokenAsync(forceRefresh: false, cancellationToken);

        var response = await http.GetAsync("api/data", cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await EnsureTokenAsync(forceRefresh: true, cancellationToken);
            response = await http.GetAsync("api/data", cancellationToken);
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorldData>(cancellationToken: cancellationToken)
            ?? new WorldData();
    }

    private async Task EnsureTokenAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        if (!forceRefresh && accessToken is not null && tokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            return;

        var response = await http.PostAsJsonAsync("api/auth/token", new { playerId }, cancellationToken);
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("The API returned an empty token response.");

        accessToken = token.AccessToken;
        tokenExpiresAt = token.ExpiresAt;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
}
