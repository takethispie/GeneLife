using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Genelife.Api.DTOs;
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

    public async Task<MapStatus> GetMapAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendWithAuthRetryAsync(
            () => http.GetAsync("api/map", cancellationToken), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MapStatus>(cancellationToken: cancellationToken)
            ?? new MapStatus();
    }

    public async Task PlaceBuildingAsync(
        ulong networkId,
        PlaceBuildingRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendWithAuthRetryAsync(
            () => http.PostAsJsonAsync($"api/map/buildings/{networkId}/place", request, cancellationToken),
            cancellationToken);
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"Placement failed ({(int)response.StatusCode}): {body}");
    }

    public async Task<SimulationStatus> GetSimulationStatusAsync(CancellationToken cancellationToken = default)    {
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

    public Task UpdateSchoolAsync(
        ulong schoolId,
        int? capacity = null,
        float? quality = null,
        decimal? monthlyCost = null,
        string? emphasis = null,
        CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Patch,
            $"api/schools/{schoolId}",
            new { capacity, quality, monthlyCost, emphasis },
            cancellationToken);

    public Task EnrollAsync(ulong schoolId, ulong simId, CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            $"api/schools/{schoolId}/enroll/{simId}",
            null,
            cancellationToken);

    public Task UnenrollAsync(ulong schoolId, ulong simId, CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Delete,
            $"api/schools/{schoolId}/enroll/{simId}",
            null,
            cancellationToken);

    public Task PromoteAsync(ulong simId, CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            $"api/employment/{simId}/promote",
            null,
            cancellationToken);

    public Task ForceDeathAsync(
        ulong simId,
        string cause = "Other",
        CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            $"api/lifecycle/sims/{simId}/death",
            new { cause },
            cancellationToken);

    public Task SetBenefitEligibilityAsync(
        ulong simId,
        bool eligible,
        CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            $"api/lifecycle/unemployment/{simId}/eligibility",
            new { eligible },
            cancellationToken);

    public Task TopUpPensionFundAsync(decimal amount, CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            "api/lifecycle/pensions/top-up",
            new { amount },
            cancellationToken);

    public Task AdvanceAgeAsync(ulong simId, int years = 5, CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            $"api/sims/{simId}/advance-age",
            new { years },
            cancellationToken);

    public Task PausePopulationAsync(CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(HttpMethod.Post, "api/lifecycle/population/pause", null, cancellationToken);

    public Task ResumePopulationAsync(CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(HttpMethod.Post, "api/lifecycle/population/resume", null, cancellationToken);

    public Task TriggerImmigrationWaveAsync(
        int count = 10,
        CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            "api/lifecycle/population/wave",
            new { count },
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

    public Task<ConsumptionOverviewData> GetConsumptionOverviewAsync(CancellationToken cancellationToken = default) =>
        GetAdminAsync<ConsumptionOverviewData>("api/consumption", cancellationToken);

    public Task<ConsumptionHouseholdPageData> GetConsumptionHouseholdsAsync(
        int take = 100,
        int skip = 0,
        string? level = null,
        CancellationToken cancellationToken = default)
    {
        var query = $"api/consumption/households?take={take}&skip={skip}";
        if (!string.IsNullOrWhiteSpace(level))
            query += $"&level={Uri.EscapeDataString(level)}";
        return GetAdminAsync<ConsumptionHouseholdPageData>(query, cancellationToken);
    }

    public Task<ConsumptionStorePageData> GetConsumptionStoresAsync(
        int take = 100,
        int skip = 0,
        CancellationToken cancellationToken = default) =>
        GetAdminAsync<ConsumptionStorePageData>($"api/consumption/stores?take={take}&skip={skip}", cancellationToken);

    public Task RestockStoreAsync(
        ulong storeNetworkId,
        string reason,
        CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            $"api/consumption/stores/{storeNetworkId}/restock",
            new { reason },
            cancellationToken);

    public Task RepairUtilityNetworkAsync(
        string service,
        int units,
        string reason,
        CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            $"api/consumption/networks/{Uri.EscapeDataString(service)}/repair",
            new { units, reason },
            cancellationToken);

    public Task<AppearancePageData> GetAppearancePageAsync(
        string? search = null,
        int take = 100,
        int skip = 0,
        CancellationToken cancellationToken = default)
    {
        var query = $"api/appearance?take={take}&skip={skip}";
        if (!string.IsNullOrWhiteSpace(search))
            query += $"&search={Uri.EscapeDataString(search)}";
        return GetAdminAsync<AppearancePageData>(query, cancellationToken);
    }

    public Task<AppearanceSimData> GetAppearanceAsync(
        ulong simNetworkId,
        CancellationToken cancellationToken = default) =>
        GetAdminAsync<AppearanceSimData>($"api/appearance/{simNetworkId}", cancellationToken);

    public Task<AppearanceFamilyData> GetAppearanceFamilyAsync(
        ulong simNetworkId,
        CancellationToken cancellationToken = default) =>
        GetAdminAsync<AppearanceFamilyData>($"api/appearance/{simNetworkId}/family", cancellationToken);

    public Task<PlayerOverviewData> GetPlayerOverviewAsync(CancellationToken cancellationToken = default) =>
        GetAdminAsync<PlayerOverviewData>("api/player", cancellationToken);

    public Task<PlayerLifePageData> GetPlayerLivesAsync(
        int take = 100,
        int skip = 0,
        string? search = null,
        string? state = null,
        CancellationToken cancellationToken = default)
    {
        var query = $"api/player/lives?take={take}&skip={skip}";
        if (!string.IsNullOrWhiteSpace(search))
            query += $"&search={Uri.EscapeDataString(search)}";
        if (!string.IsNullOrWhiteSpace(state))
            query += $"&state={Uri.EscapeDataString(state)}";
        return GetAdminAsync<PlayerLifePageData>(query, cancellationToken);
    }

    public Task<PlayerLifeData> GetPlayerLifeAsync(
        string playerId,
        CancellationToken cancellationToken = default) =>
        GetAdminAsync<PlayerLifeData>($"api/player/lives/{Uri.EscapeDataString(playerId)}", cancellationToken);

    public Task SetPlayerContinuationAsync(
        string playerId,
        string policy,
        string reason,
        CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            $"api/player/{Uri.EscapeDataString(playerId)}/continuation",
            new { policy, reason },
            cancellationToken);

    public Task DetachPlayerAsync(
        string playerId,
        string reason,
        CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            $"api/player/{Uri.EscapeDataString(playerId)}/detach",
            new { reason },
            cancellationToken);

    public Task<FamilyData> GetFamilyAsync(CancellationToken cancellationToken = default) =>
        GetAdminAsync<FamilyData>("api/family", cancellationToken);

    public Task<FamilySimPageData> GetFamilySimsAsync(
        int take = 100,
        int skip = 0,
        string? stage = null,
        bool? partnered = null,
        bool? hasChildren = null,
        ulong? householdId = null,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        var query = $"api/family/sims?take={take}&skip={skip}";
        if (!string.IsNullOrWhiteSpace(stage))
            query += $"&stage={Uri.EscapeDataString(stage)}";
        if (partnered is not null)
            query += $"&partnered={partnered.Value.ToString().ToLowerInvariant()}";
        if (hasChildren is not null)
            query += $"&hasChildren={hasChildren.Value.ToString().ToLowerInvariant()}";
        if (householdId is not null)
            query += $"&householdId={householdId.Value}";
        if (!string.IsNullOrWhiteSpace(name))
            query += $"&name={Uri.EscapeDataString(name)}";
        return GetAdminAsync<FamilySimPageData>(query, cancellationToken);
    }

    public Task<FamilySimDetailData> GetFamilySimAsync(ulong simId, CancellationToken cancellationToken = default) =>
        GetAdminAsync<FamilySimDetailData>($"api/family/sims/{simId}", cancellationToken);

    public Task<FamilyRelationshipPageData> GetFamilyRelationshipsAsync(
        ulong? simId = null,
        string? kind = null,
        string? source = null,
        int take = 200,
        int skip = 0,
        CancellationToken cancellationToken = default)
    {
        var query = $"api/family/relationships?take={take}&skip={skip}";
        if (simId is not null)
            query += $"&simId={simId.Value}";
        if (!string.IsNullOrWhiteSpace(kind))
            query += $"&kind={Uri.EscapeDataString(kind)}";
        if (!string.IsNullOrWhiteSpace(source))
            query += $"&source={Uri.EscapeDataString(source)}";
        return GetAdminAsync<FamilyRelationshipPageData>(query, cancellationToken);
    }

    public Task<FamilyPregnancyPageData> GetFamilyPregnanciesAsync(
        bool activeOnly = false,
        int take = 200,
        int skip = 0,
        CancellationToken cancellationToken = default) =>
        GetAdminAsync<FamilyPregnancyPageData>(
            $"api/family/pregnancies?activeOnly={activeOnly.ToString().ToLowerInvariant()}&take={take}&skip={skip}",
            cancellationToken);

    public Task<FamilyChildPageData> GetFamilyChildrenAsync(
        string? ageBand = null,
        ulong? guardianId = null,
        int take = 200,
        int skip = 0,
        CancellationToken cancellationToken = default)
    {
        var query = $"api/family/children?take={take}&skip={skip}";
        if (!string.IsNullOrWhiteSpace(ageBand))
            query += $"&ageBand={Uri.EscapeDataString(ageBand)}";
        if (guardianId is not null)
            query += $"&guardianId={guardianId.Value}";
        return GetAdminAsync<FamilyChildPageData>(query, cancellationToken);
    }

    public Task<List<FamilyGuardianshipData>> GetFamilyGuardianshipsAsync(
        CancellationToken cancellationToken = default) =>
        GetAdminAsync<List<FamilyGuardianshipData>>("api/family/guardianships", cancellationToken);

    public Task<FamilyAttractionData> GetFamilyAttractionAsync(
        ulong simId,
        ulong candidateId,
        CancellationToken cancellationToken = default) =>
        GetAdminAsync<FamilyAttractionData>(
            $"api/family/attraction?simId={simId}&candidateId={candidateId}",
            cancellationToken);

    public Task ForcePartnershipAsync(
        ulong simAId,
        ulong simBId,
        string reason,
        bool overrideEligibility = false,
        CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            "api/family/partnerships/force",
            new { simAId, simBId, reason, overrideEligibility },
            cancellationToken);

    public Task EndPartnershipAsync(
        ulong simAId,
        ulong simBId,
        string reason,
        CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            "api/family/partnerships/end",
            new { simAId, simBId, reason },
            cancellationToken);

    public Task StartPregnancyAsync(
        ulong carryingParentId,
        ulong otherParentId,
        string reason,
        CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            "api/family/pregnancies",
            new { carryingParentId, otherParentId, reason },
            cancellationToken);

    public Task CancelPregnancyAsync(
        ulong pregnancyId,
        string reason,
        CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            $"api/family/pregnancies/{pregnancyId}/cancel",
            new { reason },
            cancellationToken);

    public Task AssignGuardianAsync(
        ulong childId,
        ulong guardianId,
        string reason,
        CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            "api/family/guardianships",
            new { childId, guardianId, reason },
            cancellationToken);

    public Task ReleaseTrustAsync(
        ulong trustId,
        string reason,
        CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            $"api/family/trusts/{trustId}/release",
            new { reason },
            cancellationToken);

    private async Task<T> GetAdminAsync<T>(string path, CancellationToken cancellationToken)
    {
        await EnsureAdminTokenAsync(cancellationToken);
        using var response = await SendWithAuthRetryAsync(
            () => http.GetAsync(path, cancellationToken), cancellationToken);

        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException(LocalAdminUnavailableMessage);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? Activator.CreateInstance<T>();
    }

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

public sealed record PlaceBuildingRequest(
    int X,
    int Y,
    int Width,
    int Height,
    int EntryX,
    int EntryY);
