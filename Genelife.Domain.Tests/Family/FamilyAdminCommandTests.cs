using Arch.Core;
using Genelife.Api.Commands;
using Genelife.Api.Repositories;
using Genelife.Api.Services;
using Genelife.Components.Family;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Enums;
using Genelife.Family;
using Genelife.Values;

namespace Genelife.UnitTests.Family;

public class FamilyAdminCommandTests
{
    [Fact]
    public void ForcePartnershipAndEndPartnershipApplyOnTheNextTick()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        var ids = CreatePair(engine, femaleFirst: true);

        var force = new ForcePartnershipCommand(
            Guid.NewGuid(), "tester", ids.First, ids.Second, false, "test-force");
        var forceId = engine.EnqueueAdminCommand(force);
        engine.Tick(1f / 20f);

        var forceResult = engine.GetCommandResult(forceId);
        Assert.NotNull(forceResult);
        Assert.True(forceResult!.Accepted, forceResult.Error);
        Assert.Equal(1, engine.BuildFamilyStatus().Partnerships);

        var end = new EndPartnershipCommand(
            Guid.NewGuid(), "tester", ids.First, ids.Second, "test-end");
        var endId = engine.EnqueueAdminCommand(end);
        engine.Tick(1f / 20f);

        var endResult = engine.GetCommandResult(endId);
        Assert.NotNull(endResult);
        Assert.True(endResult!.Accepted, endResult.Error);
        Assert.Equal(0, engine.BuildFamilyStatus().Partnerships);
        Assert.Equal(1, engine.BuildFamilyStatus().ExPartnerships);
    }

    [Fact]
    public void ForcePartnershipRejectsAnIneligiblePairWithoutOverride()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        var ids = CreatePair(engine, femaleFirst: false, sameSex: true);

        var command = new ForcePartnershipCommand(
            Guid.NewGuid(), "tester", ids.First, ids.Second, false, "test");
        var commandId = engine.EnqueueAdminCommand(command);
        engine.Tick(1f / 20f);

        var result = engine.GetCommandResult(commandId);
        Assert.NotNull(result);
        Assert.False(result!.Accepted);
        Assert.Contains("not eligible", result.Error);
    }

    [Fact]
    public void StartAndCancelPregnancyAreAudited()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        var ids = CreatePair(engine, femaleFirst: true);

        var start = new StartPregnancyCommand(
            Guid.NewGuid(), "tester", ids.First, ids.Second, "test-start");
        var startId = engine.EnqueueAdminCommand(start);
        engine.Tick(1f / 20f);

        var startResult = engine.GetCommandResult(startId);
        Assert.NotNull(startResult);
        Assert.True(startResult!.Accepted, startResult.Error);
        var pregnancy = Assert.Single(engine.BuildPregnancyPage().Pregnancies);
        Assert.Equal("Admin", pregnancy.Source);
        Assert.NotNull(engine.BuildPregnancy(pregnancy.NetworkId));

        var cancel = new CancelPregnancyCommand(
            Guid.NewGuid(), "tester", pregnancy.NetworkId, "test-cancel");
        var cancelId = engine.EnqueueAdminCommand(cancel);
        engine.Tick(1f / 20f);

        var cancelResult = engine.GetCommandResult(cancelId);
        Assert.NotNull(cancelResult);
        Assert.True(cancelResult!.Accepted, cancelResult.Error);
        Assert.Equal("Cancelled", engine.BuildPregnancyPage().Pregnancies[0].Status);
    }

    [Fact]
    public void AssignGuardianClearsStateCare()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        ulong childId = 0;
        ulong guardianId = 0;
        engine.ExecuteWithWorld(world =>
        {
            var repository = new GenosRepository(world, engine.NetworkIds, engine.Economy);
            childId = repository.Add("Ward", 8).Value;
            guardianId = repository.Add("Guardian", 30).Value;
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(childId), out var child));
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(guardianId), out var guardian));
            AddHousehold(engine, world, guardian);
            world.Add(child, new StateCare
            {
                SinceTick = 1,
                MonthlyCost = Money.FromUnits(25m)
            });
            return 0;
        });

        var command = new AssignGuardianCommand(
            Guid.NewGuid(), "tester", childId, guardianId, "test-guardian");
        var commandId = engine.EnqueueAdminCommand(command);
        engine.Tick(1f / 20f);

        var result = engine.GetCommandResult(commandId);
        Assert.NotNull(result);
        Assert.True(result!.Accepted, result.Error);
        Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(childId), out var restoredChild));
        Assert.False(engine.World!.Has<StateCare>(restoredChild));
        Assert.Contains(
            engine.BuildGuardianships(),
            row => row.ChildNetworkId == childId && row.GuardianNetworkId == guardianId && !row.StateCare);
    }


    [Fact]
    public void AssignGuardianRejectsADeadGuardian()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        ulong childId = 0;
        ulong guardianId = 0;
        engine.ExecuteWithWorld(world =>
        {
            var repository = new GenosRepository(world, engine.NetworkIds, engine.Economy);
            childId = repository.Add("Ward", 8).Value;
            guardianId = repository.Add("Dead Guardian", 30).Value;
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(guardianId), out var guardian));
            world.Add(guardian, new Dead(1, DeathCause.Illness));
            return 0;
        });

        var commandId = engine.EnqueueAdminCommand(new AssignGuardianCommand(
            Guid.NewGuid(), "tester", childId, guardianId, "test-dead-guardian"));
        engine.Tick(1f / 20f);

        var result = engine.GetCommandResult(commandId);
        Assert.NotNull(result);
        Assert.False(result!.Accepted);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AssignGuardianRejectsAMinorGuardian()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        ulong childId = 0;
        ulong guardianId = 0;
        engine.ExecuteWithWorld(world =>
        {
            var repository = new GenosRepository(world, engine.NetworkIds, engine.Economy);
            childId = repository.Add("Ward", 8).Value;
            guardianId = repository.Add("Young Guardian", 10).Value;
            return 0;
        });

        var commandId = engine.EnqueueAdminCommand(new AssignGuardianCommand(
            Guid.NewGuid(), "tester", childId, guardianId, "test-minor-guardian"));
        engine.Tick(1f / 20f);

        var result = engine.GetCommandResult(commandId);
        Assert.NotNull(result);
        Assert.False(result!.Accepted);
        Assert.Contains("guardian-minor", result.Error);
    }

    [Fact]
    public void AssignGuardianReplacesAnExistingGuardian()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        ulong childId = 0;
        ulong firstGuardianId = 0;
        ulong secondGuardianId = 0;
        engine.ExecuteWithWorld(world =>
        {
            var repository = new GenosRepository(world, engine.NetworkIds, engine.Economy);
            childId = repository.Add("Ward", 8).Value;
            firstGuardianId = repository.Add("First Guardian", 30).Value;
            secondGuardianId = repository.Add("Second Guardian", 31).Value;
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(firstGuardianId), out var first));
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(secondGuardianId), out var second));
            AddHousehold(engine, world, first);
            AddHousehold(engine, world, second);
            return 0;
        });

        var firstCommand = engine.EnqueueAdminCommand(new AssignGuardianCommand(
            Guid.NewGuid(), "tester", childId, firstGuardianId, "first"));
        engine.Tick(1f / 20f);
        Assert.True(engine.GetCommandResult(firstCommand)!.Accepted);

        var secondCommand = engine.EnqueueAdminCommand(new AssignGuardianCommand(
            Guid.NewGuid(), "tester", childId, secondGuardianId, "second"));
        engine.Tick(1f / 20f);
        var result = engine.GetCommandResult(secondCommand);
        Assert.NotNull(result);
        Assert.True(result!.Accepted, result.Error);

        var rows = engine.BuildGuardianships()
            .Where(row => row.ChildNetworkId == childId && !row.StateCare)
            .ToList();
        var guardian = Assert.Single(rows);
        Assert.Equal(secondGuardianId, guardian.GuardianNetworkId);
    }

    [Fact]
    public void ReleaseTrustPaysTheBeneficiaryAndRemovesTheTrust()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        ulong childId = 0;
        ulong trustId = 0;
        engine.ExecuteWithWorld(world =>
        {
            var repository = new GenosRepository(world, engine.NetworkIds, engine.Economy);
            childId = repository.Add("Heir", 10).Value;
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(childId), out var child));
            var trust = engine.Trusts!.Create(
                child, Entity.Null, Money.FromUnits(100m), 0, 1,
                18L * engine.Economy!.Clock.TicksPerYear);
            trustId = engine.NetworkIds.Allocate(trust).Value;
            return 0;
        });

        var command = new ReleaseTrustCommand(
            Guid.NewGuid(), "tester", trustId, "test-release");
        var commandId = engine.EnqueueAdminCommand(command);
        engine.Tick(1f / 20f);

        var result = engine.GetCommandResult(commandId);
        Assert.NotNull(result);
        Assert.True(result!.Accepted, result.Error);
        Assert.False(engine.NetworkIds.TryGetEntity(new NetworkId(trustId), out _));
        Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(childId), out var heir));
        Assert.Equal(Money.FromUnits(100m), engine.World!.Get<Wallet>(heir).Balance);
    }

    private static Entity AddHousehold(SimulationEngine engine, World world, Entity head)
    {
        var household = world.Create(new Household
        {
            Head = head,
            Tenure = Tenure.Sheltered,
            MemberCount = 1
        });
        world.Add(head, new HouseholdMember(household, HouseholdRole.Head));
        engine.Economy!.Housing.AddMember(head, household);
        return household;
    }

    private static (ulong First, ulong Second) CreatePair(
        SimulationEngine engine,
        bool femaleFirst,
        bool sameSex = false)
    {
        ulong firstId = 0;
        ulong secondId = 0;
        engine.ExecuteWithWorld(world =>
        {
            var repository = new GenosRepository(world, engine.NetworkIds, engine.Economy);
            firstId = repository.Add("First", 30).Value;
            secondId = repository.Add("Second", 31).Value;
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(firstId), out var first));
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(secondId), out var second));
            world.Get<Traits>(first) = new Traits
            {
                Sex = femaleFirst || sameSex ? Sex.Female : Sex.Male
            };
            world.Get<Traits>(second) = new Traits
            {
                Sex = sameSex || !femaleFirst ? Sex.Female : Sex.Male
            };
            return 0;
        });

        return (firstId, secondId);
    }
}
