using Genelife.Api.Projections;
using Genelife.Messages.Events;
using Marten.Events.Projections;

namespace Genelife.UnitTests.Marten;

/// <summary>
/// Database-free guard for the map read model. A relocation must update the
/// building document, not only append an event that no projection consumes.
/// </summary>
public class MapProjectionCoverageTests
{
    [Fact]
    public void EveryDurableMapEventHasAProjectionHandler()
    {
        var handled = typeof(MapProjection).Assembly.GetTypes()
            .Where(type => type.Namespace == "Genelife.Api.Projections")
            .Where(type => typeof(EventProjection).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods())
            .Where(method => method.Name is "Project" or "Create" or "Apply")
            .Select(method => method.GetParameters().FirstOrDefault()?.ParameterType)
            .Where(type => type is not null && type.Namespace == "Genelife.Messages.Events")
            .Select(type => type!)
            .ToHashSet();

        foreach (var eventType in new[]
                 {
                     typeof(MapGenerated),
                     typeof(BuildingPlaced),
                     typeof(BuildingRemoved),
                     typeof(BuildingRelocated)
                 })
            Assert.Contains(eventType, handled);
    }
}
