using Genelife.Api.Auth;
using Genelife.Api.Commands;
using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Genelife.Enums;
using Genelife.Values;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace Genelife.Api.Endpoints.Wolverine;

public static class HousingEndpoints
{
    [WolverineGet("/api/housing/overview")]
    public static HousingStatus Overview(SimulationEngine engine) => engine.BuildHousingStatus();

    [WolverineGet("/api/housing/properties")]
    public static List<HousingPropertyStatus> Properties(SimulationEngine engine) =>
        engine.BuildHousingProperties();

    [WolverineGet("/api/housing/households")]
    public static List<HouseholdStatus> Households(SimulationEngine engine) =>
        engine.BuildHouseholds();

    [WolverineGet("/api/housing/rent-roll")]
    public static List<HousingRentRollStatus> RentRoll(SimulationEngine engine) =>
        engine.BuildHousingRentRoll();

    [WolverineGet("/api/housing/construction")]
    public static List<HousingConstructionStatus> Construction(SimulationEngine engine) =>
        engine.BuildHousingConstructionProjects();

    [WolverineGet("/api/housing/metrics/current")]
    public static IResult CurrentMetric(SimulationEngine engine)
    {
        var history = engine.BuildHousingMetricHistory(1);
        return history.Count == 0
            ? Results.NotFound(new { message = "No housing snapshot yet." })
            : Results.Ok(history[^1]);
    }

    [WolverineGet("/api/housing/metrics/history")]
    public static List<HousingSnapshotStatus> History(SimulationEngine engine, int years = 20) =>
        engine.BuildHousingMetricHistory(Math.Clamp(years, 1, 50));

    [WolverineGet("/api/housing/health")]
    public static List<HousingHealthCheckStatus> Health(SimulationEngine engine) =>
        engine.BuildHousingStatus().Health;

    [WolverineGet("/api/housing/market")]
    public static IResult Market(SimulationEngine engine)
    {
        var status = engine.BuildHousingStatus();
        return Results.Ok(new
        {
            index = status.RentIndex,
            vacancyRate = status.VacancyRate,
            vacantUnits = status.VacantUnits,
            units = status.Units,
            evictionsThisYear = status.EvictionsThisYear,
            movesThisYear = status.MovesThisYear
        });
    }

    [WolverinePost("/api/housing/properties")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult CreateProperty(
        CreatePropertyRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (!TryParseTier(request.Tier, out var tier))
            return Results.BadRequest(new { message = "Unknown housing tier." });
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { message = "Name is required." });
        if (string.IsNullOrWhiteSpace(request.District))
            return Results.BadRequest(new { message = "District is required." });
        if (request.Units <= 0)
            return Results.BadRequest(new { message = "Units must be positive." });

        return Enqueue(engine, new CreatePropertyCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            request.OwnerId,
            request.Name,
            request.District,
            request.Units,
            tier,
            request.MonthlyRent is null ? null : Money.FromUnits(request.MonthlyRent.Value),
            request.Condition ?? 0.8f,
            request.Neighbourhood ?? 0.5f));
    }

    [WolverinePatch("/api/housing/properties/{propertyId}")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult UpdateProperty(
        ulong propertyId,
        UpdatePropertyRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        return Enqueue(engine, new UpdatePropertyCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            propertyId,
            request.Name,
            request.Condition,
            request.MonthlyUpkeep is null ? null : Money.FromUnits(request.MonthlyUpkeep.Value)));
    }

    [WolverinePatch("/api/housing/units/{unitId}")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult UpdateUnit(
        ulong unitId,
        UpdateHousingUnitRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        HousingTier? tier = null;
        if (request.Tier is not null)
        {
            if (!TryParseTier(request.Tier, out var parsed))
                return Results.BadRequest(new { message = "Unknown housing tier." });
            tier = parsed;
        }

        if (request.Capacity is <= 0)
            return Results.BadRequest(new { message = "Capacity must be positive." });
        if (request.BaseRent is < 0)
            return Results.BadRequest(new { message = "Base rent cannot be negative." });

        return Enqueue(engine, new UpdateHousingUnitCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            unitId,
            tier,
            request.Capacity,
            request.BaseRent is null ? null : Money.FromUnits(request.BaseRent.Value),
            request.Condition));
    }

    [WolverinePost("/api/housing/properties/{propertyId}/repair")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult RepairProperty(
        ulong propertyId,
        RepairPropertyRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (request.Amount <= 0)
            return Results.BadRequest(new { message = "Repair amount must be positive." });

        return Enqueue(engine, new RepairPropertyCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            propertyId,
            Money.FromUnits(request.Amount)));
    }

    [WolverinePost("/api/housing/assign")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Assign(
        AssignHousingRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        return Enqueue(engine, new AssignHousingCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            request.HouseholdId,
            request.UnitId));
    }

    [WolverinePost("/api/housing/households/{householdId}/evict")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Evict(
        ulong householdId,
        EvictRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        return Enqueue(engine, new EvictHouseholdCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            householdId,
            request.Immediate));
    }

    [WolverinePost("/api/housing/construction")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult StartConstruction(
        ConstructionRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (!TryParseTier(request.Tier, out var tier))
            return Results.BadRequest(new { message = "Unknown housing tier." });
        if (string.IsNullOrWhiteSpace(request.District))
            return Results.BadRequest(new { message = "District is required." });
        if (request.Units <= 0)
            return Results.BadRequest(new { message = "Units must be positive." });

        return Enqueue(engine, new StartConstructionCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            request.DeveloperId,
            request.District,
            tier,
            request.Units));
    }

    [WolverineDelete("/api/housing/construction/{projectId}")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult CancelConstruction(ulong projectId, SimulationEngine engine, HttpContext http)
    {
        return Enqueue(engine, new CancelConstructionCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            projectId));
    }

    [WolverinePatch("/api/housing/market/index")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult SetRentIndex(
        RentIndexRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (request.Index <= 0)
            return Results.BadRequest(new { message = "Rent index must be positive." });

        return Enqueue(engine, new SetRentIndexCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            request.Index));
    }

    [WolverinePost("/api/population/spawn")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult SpawnPopulation(
        SpawnPopulationRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (request.Adults < 0 || request.Children < 0)
            return Results.BadRequest(new { message = "Population counts cannot be negative." });
        if (request.ExpectedMonthlyIncome < 0)
            return Results.BadRequest(new { message = "Expected income cannot be negative." });

        return Enqueue(engine, new SpawnPopulationCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            request.Adults,
            request.Children,
            request.ExpectedMonthlyIncome,
            request.ShelterBeds,
            request.BasicUnits,
            request.ComfortableUnits,
            request.LuxuryUnits,
            request.Seed));
    }

    [WolverinePost("/api/housing/households")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult CreateHousehold(
        CreateHouseholdRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        return Enqueue(engine, new CreateHouseholdCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            request.HeadSimId));
    }

    [WolverinePost("/api/housing/households/{householdId}/members")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult AddMember(
        ulong householdId,
        AddHouseholdMemberRequest request,
        SimulationEngine engine,
        HttpContext http)
    {
        if (!Enum.TryParse<HouseholdRole>(request.Role, ignoreCase: true, out var role))
            return Results.BadRequest(new { message = "Unknown household role." });

        return Enqueue(engine, new AddHouseholdMemberCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            householdId,
            request.SimId,
            role));
    }

    [WolverineDelete("/api/housing/households/{householdId}/members/{simId}")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult RemoveMember(
        ulong householdId,
        ulong simId,
        SimulationEngine engine,
        HttpContext http)
    {
        return Enqueue(engine, new RemoveHouseholdMemberCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            householdId,
            simId));
    }

    [WolverinePost("/api/housing/purchase")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Purchase(PurchaseHomeRequest request, SimulationEngine engine, HttpContext http)
    {
        return Enqueue(engine, new PurchaseHomeCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            request.HouseholdId,
            request.UnitId));
    }

    [WolverinePost("/api/housing/sell")]
    [Authorize(Policy = AuthPolicies.Admin)]
    public static IResult Sell(SellHomeRequest request, SimulationEngine engine, HttpContext http)
    {
        return Enqueue(engine, new SellHomeCommand(
            Guid.NewGuid(),
            http.User.Identity?.Name ?? "admin",
            request.HouseholdId,
            request.UnitId,
            request.BuyerHouseholdId));
    }

    private static bool TryParseTier(string value, out HousingTier tier) =>
        Enum.TryParse(value, ignoreCase: true, out tier);

    private static IResult Enqueue(SimulationEngine engine, AdminCommand command)
    {
        var commandId = engine.EnqueueAdminCommand(command);
        return Results.Accepted($"/api/commands/{commandId}", new { commandId });
    }
}

public sealed record CreatePropertyRequest(
    ulong OwnerId,
    string Name,
    string District,
    int Units,
    string Tier,
    decimal? MonthlyRent = null,
    float? Condition = 0.8f,
    float? Neighbourhood = 0.5f);

public sealed record UpdatePropertyRequest(string? Name, float? Condition, decimal? MonthlyUpkeep);

public sealed record UpdateHousingUnitRequest(
    string? Tier,
    int? Capacity,
    decimal? BaseRent,
    float? Condition);

public sealed record RepairPropertyRequest(decimal Amount);

public sealed record AssignHousingRequest(ulong HouseholdId, ulong UnitId);

public sealed record EvictRequest(bool Immediate = false);

public sealed record ConstructionRequest(
    ulong DeveloperId,
    string District,
    string Tier,
    int Units);

public sealed record RentIndexRequest(decimal Index);

public sealed record SpawnPopulationRequest(
    int Adults,
    int Children,
    decimal ExpectedMonthlyIncome = 300m,
    int ShelterBeds = 0,
    int BasicUnits = 0,
    int ComfortableUnits = 0,
    int LuxuryUnits = 0,
    int Seed = 1);

public sealed record CreateHouseholdRequest(ulong HeadSimId);

public sealed record AddHouseholdMemberRequest(ulong SimId, string Role = "Partner");

public sealed record PurchaseHomeRequest(ulong HouseholdId, ulong UnitId);

public sealed record SellHomeRequest(ulong HouseholdId, ulong UnitId, ulong BuyerHouseholdId);
