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

    public async Task<EconomyData> GetEconomyAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendWithAuthRetryAsync(
            () => http.GetAsync("api/economy/overview", cancellationToken), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EconomyData>(cancellationToken: cancellationToken)
            ?? new EconomyData();
    }

    public async Task<FiscalData> GetFiscalAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendWithAuthRetryAsync(
            () => http.GetAsync("api/economy/fiscal", cancellationToken), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FiscalData>(cancellationToken: cancellationToken)
            ?? new FiscalData();
    }

    public async Task<List<EconomySnapshotData>> GetEconomyHistoryAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendWithAuthRetryAsync(
            () => http.GetAsync("api/economy/metrics/history", cancellationToken), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<EconomySnapshotData>>(cancellationToken: cancellationToken)
            ?? new List<EconomySnapshotData>();
    }

    public async Task<List<LedgerEntryData>> GetLedgerAsync(int take = 200, CancellationToken cancellationToken = default)
    {
        using var response = await SendWithAuthRetryAsync(
            () => http.GetAsync($"api/economy/ledger?take={take}", cancellationToken), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<LedgerEntryData>>(cancellationToken: cancellationToken)
            ?? new List<LedgerEntryData>();
    }

    public async Task<HousingData> GetHousingAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendWithAuthRetryAsync(
            () => http.GetAsync("api/housing/overview", cancellationToken), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<HousingData>(cancellationToken: cancellationToken)
            ?? new HousingData();
    }

    public async Task<List<HousingPropertyData>> GetHousingPropertiesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendWithAuthRetryAsync(
            () => http.GetAsync("api/housing/properties", cancellationToken), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<HousingPropertyData>>(cancellationToken: cancellationToken)
            ?? new List<HousingPropertyData>();
    }

    public async Task<List<HouseholdData>> GetHouseholdsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendWithAuthRetryAsync(
            () => http.GetAsync("api/housing/households", cancellationToken), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<HouseholdData>>(cancellationToken: cancellationToken)
            ?? new List<HouseholdData>();
    }

    public async Task<List<HousingRentRollData>> GetHousingRentRollAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendWithAuthRetryAsync(
            () => http.GetAsync("api/housing/rent-roll", cancellationToken), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<HousingRentRollData>>(cancellationToken: cancellationToken)
            ?? new List<HousingRentRollData>();
    }

    public async Task<List<HousingConstructionData>> GetHousingConstructionAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendWithAuthRetryAsync(
            () => http.GetAsync("api/housing/construction", cancellationToken), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<HousingConstructionData>>(cancellationToken: cancellationToken)
            ?? new List<HousingConstructionData>();
    }

    public async Task<List<HousingSnapshotData>> GetHousingHistoryAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendWithAuthRetryAsync(
            () => http.GetAsync("api/housing/metrics/history", cancellationToken), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<HousingSnapshotData>>(cancellationToken: cancellationToken)
            ?? new List<HousingSnapshotData>();
    }

    public Task CreatePropertyAsync(
        string name,
        string district,
        int units,
        string tier,
        decimal? monthlyRent,
        CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            "api/housing/properties",
            new { ownerId = 0UL, name, district, units, tier, monthlyRent, condition = 0.8f, neighbourhood = 0.5f },
            cancellationToken);

    public Task SetRentIndexAsync(decimal index, CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(HttpMethod.Patch, "api/housing/market/index", new { index }, cancellationToken);

    public Task AssignHousingAsync(ulong householdId, ulong unitId, CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(HttpMethod.Post, "api/housing/assign", new { householdId, unitId }, cancellationToken);

    public Task RepairPropertyAsync(ulong propertyId, decimal amount, CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            $"api/housing/properties/{propertyId}/repair",
            new { amount },
            cancellationToken);

    public Task EvictAsync(ulong householdId, bool immediate = true, CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            $"api/housing/households/{householdId}/evict",
            new { immediate },
            cancellationToken);

    public Task SpawnPopulationAsync(
        int adults,
        int children,
        decimal expectedMonthlyIncome = 300m,
        int seed = 1,
        CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            "api/population/spawn",
            new
            {
                adults,
                children,
                expectedMonthlyIncome,
                shelterBeds = 0,
                basicUnits = 0,
                comfortableUnits = 0,
                luxuryUnits = 0,
                seed
            },
            cancellationToken);

    public async Task<LifecycleData> GetLifecycleAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendWithAuthRetryAsync(
            () => http.GetAsync("api/lifecycle/overview", cancellationToken), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LifecycleData>(cancellationToken: cancellationToken)
            ?? new LifecycleData();
    }

    public async Task<List<SimLifecycleData>> GetLifecycleSimsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendWithAuthRetryAsync(
            () => http.GetAsync("api/lifecycle/sims", cancellationToken), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<SimLifecycleData>>(cancellationToken: cancellationToken)
            ?? new List<SimLifecycleData>();
    }

    public Task CreateSchoolAsync(
        string name,
        string district,
        int capacity,
        string emphasis,
        decimal monthlyCost,
        CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            "api/lifecycle/schools",
            new { name, district, capacity, quality = 0.65f, emphasis, monthlyCost },
            cancellationToken);

    public Task GraduateAsync(ulong simId, CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(HttpMethod.Post, $"api/lifecycle/sims/{simId}/graduate", null, cancellationToken);

    public Task RetireAsync(ulong simId, CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(HttpMethod.Post, $"api/lifecycle/sims/{simId}/retire", null, cancellationToken);

    public Task ForceStageAsync(ulong simId, string stage, CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            $"api/lifecycle/sims/{simId}/stage",
            new { stage },
            cancellationToken);

    public Task GrantCareAsync(ulong simId, float healthGain = 25f, CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            $"api/lifecycle/sims/{simId}/care",
            new { healthGain },
            cancellationToken);

    public Task GeneratePopulationAsync(
        int totalSims,
        int seed = 1,
        int? adults = null,
        int? children = null,
        CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            "api/lifecycle/generate",
            new { totalSims, seed, adults, children },
            cancellationToken);

    public Task SetPopulationTargetAsync(
        int targetPopulation,
        bool enabled = true,
        CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            "api/lifecycle/population/target",
            new { targetPopulation, enabled },
            cancellationToken);

    private async Task SendAdminJsonAsync(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        await EnsureAdminTokenAsync(cancellationToken);

        using var response = await SendWithAuthRetryAsync(
            () =>
            {
                var request = new HttpRequestMessage(method, path);
                if (body is not null)
                    request.Content = JsonContent.Create(body);
                return http.SendAsync(request, cancellationToken);
            },
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException(LocalAdminUnavailableMessage);

        response.EnsureSuccessStatusCode();
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
