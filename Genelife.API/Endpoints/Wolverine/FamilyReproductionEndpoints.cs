using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Wolverine.Http;

namespace Genelife.Api.Endpoints.Wolverine;

public static class FamilyReproductionEndpoints
{
    [WolverineGet("/api/family/pregnancies")]
    public static List<PregnancyStatusDto> Pregnancies(SimulationEngine engine) =>
        engine.BuildPregnancies();

    [WolverineGet("/api/family/pregnancies/{id}")]
    public static PregnancyStatusDto? Pregnancy(SimulationEngine engine, ulong id) =>
        engine.BuildPregnancies().FirstOrDefault(pregnancy => pregnancy.NetworkId == id);

    [WolverineGet("/api/family/children")]
    public static List<ChildStatusDto> Children(SimulationEngine engine) =>
        engine.BuildChildren();

    [WolverineGet("/api/family/children/{id}")]
    public static ChildStatusDto? Child(SimulationEngine engine, ulong id) =>
        engine.BuildChildren().FirstOrDefault(child => child.NetworkId == id);

    [WolverineGet("/api/family/guardianships")]
    public static List<GuardianshipStatusDto> Guardianships(SimulationEngine engine) =>
        engine.BuildGuardianships();
}
