using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Wolverine.Http;

namespace Genelife.Api.Endpoints.Wolverine;

public static class FamilyEndpoints
{
    [WolverineGet("/api/family")]
    public static FamilyStatus Status(SimulationEngine engine) => engine.BuildFamilyStatus();

    [WolverineGet("/api/family/relationships")]
    public static FamilyRelationshipPage Relationships(
        SimulationEngine engine,
        ulong? simId = null,
        string? kind = null,
        string? source = null,
        int take = 100,
        int skip = 0) =>
        engine.BuildFamilyRelationships(simId, kind, source, take, skip);

    [WolverineGet("/api/family/attraction")]
    public static AttractionExplanationStatus Attraction(
        SimulationEngine engine,
        ulong simId,
        ulong candidateId) =>
        engine.BuildAttractionExplanation(simId, candidateId);

    [WolverineGet("/api/family/health")]
    public static List<LifecycleHealthCheckStatus> Health(SimulationEngine engine) =>
        engine.BuildFamilyStatus().Health;
}
