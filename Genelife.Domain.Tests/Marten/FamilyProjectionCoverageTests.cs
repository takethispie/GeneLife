using Genelife.Api.Projections;
using Genelife.Messages.Events;
using Marten.Events.Projections;

namespace Genelife.UnitTests.Marten;

/// <summary>
/// Cheap, database-free guard that every durable family event still has a
/// projection handler. Marten integration tests need PostgreSQL, but a missing
/// handler should fail in every environment.
/// </summary>
public class FamilyProjectionCoverageTests
{
    [Fact]
    public void EveryDurableFamilyEventHasAProjectionHandler()
    {
        var projectionTypes = typeof(FamilyProjection).Assembly.GetTypes()
            .Where(type => type.Namespace == "Genelife.Api.Projections")
            .Where(type => typeof(EventProjection).IsAssignableFrom(type))
            .ToList();

        var handled = projectionTypes
            .SelectMany(type => type.GetMethods())
            .Where(method => method.Name is "Project" or "Create" or "Apply")
            .Select(method => method.GetParameters().FirstOrDefault()?.ParameterType)
            .Where(type => type is not null && type.Namespace == "Genelife.Messages.Events")
            .Select(type => type!)
            .ToHashSet();

        var required = new[]
        {
            typeof(SimCreated),
            typeof(SimDied),
            typeof(RelationshipFormed),
            typeof(RelationshipEnded),
            typeof(PartnershipFormed),
            typeof(PartnershipEnded),
            typeof(ChildBorn),
            typeof(BirthRecorded),
            typeof(GuardianshipAssigned),
            typeof(StateCareOpened),
            typeof(StateCareClosed),
            typeof(PregnancyStarted),
            typeof(PregnancyProgressed),
            typeof(PregnancyLost),
            typeof(PregnancyCancelled),
            typeof(TrustOpened),
            typeof(TrustReleased),
            typeof(InheritancePaid),
            typeof(EstateSettled),
            typeof(EscheatRecorded),
            typeof(HouseholdMerged),
            typeof(HouseholdSplit),
            typeof(FamilyAdminActionApplied)
        };

        foreach (var eventType in required)
            Assert.Contains(eventType, handled);
    }
}
