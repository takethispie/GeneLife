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
    private bool tokenIsAdmin;
    private bool localAdminUnavailable;

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
        using var response = await SendWithAuthRetryAsync(
            () => http.GetAsync("api/data", cancellationToken), cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorldData>(cancellationToken: cancellationToken)
            ?? new WorldData();
    }

    public async Task<SimulationStatus> GetSimulationStatusAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendWithAuthRetryAsync(
            () => http.GetAsync("api/simulation/status", cancellationToken), cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SimulationStatus>(cancellationToken: cancellationToken)
            ?? new SimulationStatus();
    }

    public Task StartSimulationAsync(CancellationToken cancellationToken = default)
        => SendAdminCommandAsync("api/simulation/start", cancellationToken);

    public Task StopSimulationAsync(CancellationToken cancellationToken = default)
        => SendAdminCommandAsync("api/simulation/stop", cancellationToken);

    private async Task SendAdminCommandAsync(string path, CancellationToken cancellationToken)
    {
        await EnsureAdminTokenAsync(cancellationToken);

        using var response = await SendWithAuthRetryAsync(
            () => http.PostAsync(path, content: null, cancellationToken), cancellationToken);

        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException(LocalAdminUnavailableMessage);

        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpResponseMessage> SendWithAuthRetryAsync(
        Func<Task<HttpResponseMessage>> send,
        CancellationToken cancellationToken)
    {
        await EnsureTokenAsync(forceRefresh: false, cancellationToken);

        var response = await send();
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            await EnsureTokenAsync(forceRefresh: true, cancellationToken);
            response = await send();
        }

        return response;
    }

    private async Task EnsureTokenAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        if (!forceRefresh && accessToken is not null && tokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            return;

        if (await TryIssueLocalAdminTokenAsync(cancellationToken))
            return;

        await IssuePlayerTokenAsync(cancellationToken);
    }

    private async Task EnsureAdminTokenAsync(CancellationToken cancellationToken)
    {
        if (tokenIsAdmin && accessToken is not null && tokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            return;

        if (await TryIssueLocalAdminTokenAsync(cancellationToken))
            return;

        throw new UnauthorizedAccessException(LocalAdminUnavailableMessage);
    }

    private async Task<bool> TryIssueLocalAdminTokenAsync(CancellationToken cancellationToken)
    {
        if (localAdminUnavailable)
            return false;

        using var response = await http.PostAsync("api/auth/local-admin", content: null, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            localAdminUnavailable = true;
            return false;
        }

        response.EnsureSuccessStatusCode();
        await ApplyTokenAsync(response, isAdmin: true, cancellationToken);
        return true;
    }

    private async Task IssuePlayerTokenAsync(CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync("api/auth/token", new { playerId }, cancellationToken);
        response.EnsureSuccessStatusCode();
        await ApplyTokenAsync(response, isAdmin: false, cancellationToken);
    }

    private async Task ApplyTokenAsync(
        HttpResponseMessage response,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("The API returned an empty token response.");

        accessToken = token.AccessToken;
        tokenExpiresAt = token.ExpiresAt;
        tokenIsAdmin = isAdmin;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private const string LocalAdminUnavailableMessage =
        "Local admin access is disabled. Enable Auth:Jwt:AllowLocalAdmin on the API and run the admin UI on the same machine.";

    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
}
