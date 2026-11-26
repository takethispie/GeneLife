using Arch.Core;
using Genelife.Api.Services;
using Genelife.Application.Systems;
using Genelife.Components.Gen;
using Genelife.Enums;
using Genelife.Values;

namespace Genelife.UnitTests.Economy;

/// <summary>
/// PR-time guardrails for the economy invariants that were previously only
/// <em>reported</em> (nightly performance job) or spot-checked. These run in the
/// fast tier on every push and cost no wall-clock, so a regression fails loudly
/// at PR time instead of surfacing days later.
///
/// 1. <b>Money conservation</b> — the total money supply must move by exactly the
///    ledger's own net (sources − sinks). Any leak (a transfer that debits
///    without crediting, or a mint without a ledger entry) shows up as a non-zero
///    gap here.
/// 2. <b>Need restoration</b> — a need is brought to exactly 100 by the
///    action-completion branch. The in-flight trickle alone leaves it short for a
///    normal-length action (Eating from 5 gains only ~40 over its 5 s), so a
///    second writer that clobbers the action before it completes is what silently
///    starves a genos. Both directions are asserted, plus an end-to-end check that
///    restoration actually happens at scale.
/// </summary>
public class EconomyInvariantGuardrailTests
{
    private const float DeltaTime = 1f / 20f;

    [Theory]
    [InlineData(60, 21)]
    [InlineData(60, 99)]
    [InlineData(120, 7)]
    public void MoneySupply_MovesExactlyByTheLedgerNet(int genoses, int seed)
    {
        using var engine = Seeded(genoses, seed);

        var before = engine.Economy!.TotalMoneySupply();
        var sourceBefore = engine.Economy.Ledger.SourceTotal;
        var sinkBefore = engine.Economy.Ledger.SinkTotal;

        var ticks = engine.Economy.Clock.TicksPerYear;
        for (var i = 0; i < ticks; i++)
            engine.Tick(DeltaTime);

        var after = engine.Economy.TotalMoneySupply();
        var conservationGap = (after - before) -
                              ((engine.Economy.Ledger.SourceTotal - sourceBefore) -
                               (engine.Economy.Ledger.SinkTotal - sinkBefore));

        Assert.Equal(Money.Zero, conservationGap);
    }

    [Fact]
    public void NeedReachesFull_OnlyWhenTheActionCompletes()
    {
        var world = World.Create();
        var genos = world.Create(
            new Needs { Hunger = 5f, Energy = 100f, Hygiene = 100f, Bladder = 100f },
            new CurrentAction(ActionType.Eating, 5f));
        var action = new ActionSystem(world);

        // In-flight: the trickle (8/s) must not be able to top hunger up to 100.
        ((ISimulationSystem)action).Update(new SimulationContext(1, DeltaTime));
        Assert.True(world.Get<Needs>(genos).Hunger < 100f,
            "the in-flight trickle restored hunger to full - a need must only reach 100 on completion");

        // Run to completion, then assert the full restore happened.
        for (var t = 0; t < 1_000 && world.Get<CurrentAction>(genos).Type != ActionType.Idle; t++)
            ((ISimulationSystem)action).Update(new SimulationContext(t + 2, DeltaTime));

        Assert.Equal(ActionType.Idle, world.Get<CurrentAction>(genos).Type);
        Assert.Equal(100f, world.Get<Needs>(genos).Hunger);
    }

    [Theory]
    [InlineData(ActionType.Eating, 5f)]
    [InlineData(ActionType.Sleeping, 8f)]
    [InlineData(ActionType.Showering, 3f)]
    [InlineData(ActionType.UsingToilet, 2f)]
    public void CompletedAction_RestoresItsNeedToExactlyFull(ActionType action, float duration)
    {
        var world = World.Create();
        var genos = world.Create(
            new Needs { Hunger = 5f, Energy = 5f, Hygiene = 5f, Bladder = 5f },
            new CurrentAction(action, duration));
        var system = new ActionSystem(world);

        for (var t = 0; t < 2_000 && world.Get<CurrentAction>(genos).Type != ActionType.Idle; t++)
            ((ISimulationSystem)system).Update(new SimulationContext(t + 1, DeltaTime));

        Assert.Equal(ActionType.Idle, world.Get<CurrentAction>(genos).Type);
        var needs = world.Get<Needs>(genos);
        var restored = action switch
        {
            ActionType.Eating => needs.Hunger,
            ActionType.Sleeping => needs.Energy,
            ActionType.Showering => needs.Hygiene,
            ActionType.UsingToilet => needs.Bladder,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
        Assert.Equal(100f, restored);
    }

    [Fact]
    public void RealRun_RestoresNeedsToFull()
    {
        using var engine = Seeded(120, seed: 21);

        var ticks = engine.Economy!.Clock.TicksPerYear;
        for (var i = 0; i < ticks; i++)
            engine.Tick(DeltaTime);

        var full = 0;
        engine.ExecuteWithWorld(world =>
        {
            var query = new QueryDescription().WithAll<Needs>().WithNone<Dead>();
            world.Query(in query, (ref Needs needs) =>
            {
                if (needs.Hunger >= 99f)
                    full++;
            });
            return 0;
        });

        Assert.True(full > 0,
            "no genos reached full hunger in a year of simulation - need restoration is disabled");
    }

    /// <summary>
    /// The V5 rent-burden / Gini bands are deliberately NOT asserted in the PR tier.
    /// The default path currently misses them at every scale, so a hard target assert
    /// here would fail the PR gate, block all work, and mask NEW failures. The RED
    /// target assert lives in the nightly perf tier
    /// (BalanceScenarioTests.AssertBalanceBands), which is non-gating; this PR tier
    /// stays non-blocking. Replace this [Skip] with the real target assert once
    /// pedro's rent-burden rebalance lands (board: rent-burden rebalance).
    /// </summary>
    [Fact(Skip = "Known miss: rent burden 0.4041 / Gini 0.7802 > 0.40 / 0.70 (seed 21). " +
                 "RED target assert runs in the nightly perf tier; PR gate stays non-blocking. " +
                 "Owner: pedro (board: rent-burden rebalance).")]
    public void RentBurden_And_Gini_MeetV5Targets()
    {
    }

    private static SimulationEngine Seeded(int genoses, int seed)
    {
        var engine = new SimulationEngine();
        engine.Initialize();
        engine.ExecuteWithWorld(world =>
        {
            var generator = new PopulationGenerator(
                world,
                engine.NetworkIds,
                engine.Economy!,
                engine.EmploymentIndex!,
                relationshipIndex: engine.Relationships,
                relationshipService: engine.RelationshipService!,
                relationshipOptions: engine.RelationshipOptions);
            generator.Generate(genoses, seed: seed);
            return 0;
        });
        return engine;
    }
}
