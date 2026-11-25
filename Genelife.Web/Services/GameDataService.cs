using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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

    public async Task RelocateBuildingAsync(
        ulong networkId,
        RelocateBuildingRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendWithAuthRetryAsync(
            () => http.PostAsJsonAsync($"api/map/buildings/{networkId}/relocate", request, cancellationToken),
            cancellationToken);
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"Relocation failed ({(int)response.StatusCode}): {body}");
    }

    /// <summary>
    /// Creates a new world map. This is a destructive, potentially slow admin
    /// operation, so it uses a dedicated two-minute client instead of the normal
    /// ten-second admin timeout.
    /// </summary>
    public async Task<CreateMapResult> CreateMapAsync(
        CreateMapRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminTokenAsync(cancellationToken);

        using var client = new HttpClient
        {
            BaseAddress = http.BaseAddress,
            Timeout = TimeSpan.FromMinutes(2)
        };
        if (!string.IsNullOrWhiteSpace(accessToken))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.PostAsJsonAsync("api/map/create", request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new MapConfirmationRequiredException(body);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Map creation failed ({(int)response.StatusCode}): {body}");
        }

        return await response.Content.ReadFromJsonAsync<CreateMapResult>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("The API returned an empty map creation result.");
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

    public async Task<List<GenosLifecycleData>> GetLifecycleGenosesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendWithAuthRetryAsync(
            () => http.GetAsync("api/lifecycle/genoses", cancellationToken), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<GenosLifecycleData>>(cancellationToken: cancellationToken)
            ?? new List<GenosLifecycleData>();
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

    public Task GraduateAsync(ulong genosId, CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(HttpMethod.Post, $"api/lifecycle/genoses/{genosId}/graduate", null, cancellationToken);

    public Task RetireAsync(ulong genosId, CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(HttpMethod.Post, $"api/lifecycle/genoses/{genosId}/retire", null, cancellationToken);

    public Task ForceStageAsync(ulong genosId, string stage, CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            $"api/lifecycle/genoses/{genosId}/stage",
            new { stage },
            cancellationToken);

    public Task GrantCareAsync(ulong genosId, float healthGain = 25f, CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            $"api/lifecycle/genoses/{genosId}/care",
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

    public Task EnrollAsync(ulong schoolId, ulong genosId, CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            $"api/schools/{schoolId}/enroll/{genosId}",
            null,
            cancellationToken);

    public Task UnenrollAsync(ulong schoolId, ulong genosId, CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Delete,
            $"api/schools/{schoolId}/enroll/{genosId}",
            null,
            cancellationToken);

    public Task PromoteAsync(ulong genosId, CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            $"api/employment/{genosId}/promote",
            null,
            cancellationToken);

    public Task ForceDeathAsync(
        ulong genosId,
        string cause = "Other",
        CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            $"api/lifecycle/genoses/{genosId}/death",
            new { cause },
            cancellationToken);

    public Task SetBenefitEligibilityAsync(
        ulong genosId,
        bool eligible,
        CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            $"api/lifecycle/unemployment/{genosId}/eligibility",
            new { eligible },
            cancellationToken);

    public Task TopUpPensionFundAsync(decimal amount, CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            "api/lifecycle/pensions/top-up",
            new { amount },
            cancellationToken);

    public Task AdvanceAgeAsync(ulong genosId, int years = 5, CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            $"api/genoses/{genosId}/advance-age",
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
        int totalGenoses,
        int seed = 1,
        int? adults = null,
        int? children = null,
        CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            "api/lifecycle/generate",
            new { totalGenoses, seed, adults, children },
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

    public async Task<Guid> RestockStoreAsync(
        ulong storeNetworkId,
        string reason,
        CancellationToken cancellationToken = default) =>
        await SendAdminJsonForIdAsync(
            HttpMethod.Post,
            $"api/consumption/stores/{storeNetworkId}/restock",
            new { reason },
            cancellationToken);

    public async Task<(bool Accepted, string? Error, int Units)?> GetCommandResultAsync(
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminTokenAsync(cancellationToken);

        using var response = await SendWithAuthRetryAsync(
            () => http.GetAsync($"api/commands/{commandId}", cancellationToken), cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException(LocalAdminUnavailableMessage);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CommandResultResponse>(
            cancellationToken: cancellationToken);
        if (result is null)
            return null;

        var units = result.Payload.ValueKind == JsonValueKind.Object &&
                    result.Payload.TryGetProperty("units", out var unitsProperty) &&
                    unitsProperty.TryGetInt32(out var parsedUnits)
            ? parsedUnits
            : 0;
        return (result.Accepted, result.Error, units);
    }

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

    public Task<AppearanceGenosData> GetAppearanceAsync(
        ulong genosNetworkId,
        CancellationToken cancellationToken = default) =>
        GetAdminAsync<AppearanceGenosData>($"api/appearance/{genosNetworkId}", cancellationToken);

    public Task<AppearanceFamilyData> GetAppearanceFamilyAsync(
        ulong genosNetworkId,
        CancellationToken cancellationToken = default) =>
        GetAdminAsync<AppearanceFamilyData>($"api/appearance/{genosNetworkId}/family", cancellationToken);

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

    public Task<SocialAdminOverviewDto> GetSocialAdminAsync(CancellationToken cancellationToken = default) =>
        GetAdminAsync<SocialAdminOverviewDto>("api/social/admin/overview", cancellationToken);

    public Task<FamilyData> GetFamilyAsync(CancellationToken cancellationToken = default) =>
        GetAdminAsync<FamilyData>("api/family", cancellationToken);

    public Task<FamilyGenosPageData> GetFamilyGenosesAsync(
        int take = 100,
        int skip = 0,
        string? stage = null,
        bool? partnered = null,
        bool? hasChildren = null,
        ulong? householdId = null,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        var query = $"api/family/genoses?take={take}&skip={skip}";
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
        return GetAdminAsync<FamilyGenosPageData>(query, cancellationToken);
    }

    public Task<FamilyGenosDetailData> GetFamilyGenosAsync(ulong genosId, CancellationToken cancellationToken = default) =>
        GetAdminAsync<FamilyGenosDetailData>($"api/family/genoses/{genosId}", cancellationToken);

    public Task<FamilyRelationshipPageData> GetFamilyRelationshipsAsync(
        ulong? genosId = null,
        string? kind = null,
        string? source = null,
        int take = 200,
        int skip = 0,
        CancellationToken cancellationToken = default)
    {
        var query = $"api/family/relationships?take={take}&skip={skip}";
        if (genosId is not null)
            query += $"&genosId={genosId.Value}";
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
        ulong genosId,
        ulong candidateId,
        CancellationToken cancellationToken = default) =>
        GetAdminAsync<FamilyAttractionData>(
            $"api/family/attraction?genosId={genosId}&candidateId={candidateId}",
            cancellationToken);

    public Task ForcePartnershipAsync(
        ulong genosAId,
        ulong genosBId,
        string reason,
        bool overrideEligibility = false,
        CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            "api/family/partnerships/force",
            new { genosAId, genosBId, reason, overrideEligibility },
            cancellationToken);

    public Task EndPartnershipAsync(
        ulong genosAId,
        ulong genosBId,
        string reason,
        CancellationToken cancellationToken = default) =>
        SendAdminJsonAsync(
            HttpMethod.Post,
            "api/family/partnerships/end",
            new { genosAId, genosBId, reason },
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

    private async Task<Guid> SendAdminJsonForIdAsync(
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
        var accepted = await response.Content.ReadFromJsonAsync<AcceptedCommandResponse>(
            cancellationToken: cancellationToken);
        return accepted?.CommandId ?? throw new InvalidOperationException(
            "The API did not return a command id.");
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
    private sealed record AcceptedCommandResponse(Guid CommandId);
    private sealed record CommandResultResponse(Guid CommandId, bool Accepted, string? Error, JsonElement Payload);
}

public sealed record PlaceBuildingRequest(
    int X,
    int Y,
    int Width,
    int Height,
    int EntryX,
    int EntryY);

public sealed record RelocateBuildingRequest(
    int X,
    int Y,
    int EntryX,
    int EntryY);

public sealed class MapConfirmationRequiredException : Exception
{
    public MapConfirmationRequiredException(string message) : base(message)
    {
    }
}
