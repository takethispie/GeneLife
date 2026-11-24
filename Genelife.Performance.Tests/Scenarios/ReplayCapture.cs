using Arch.Core;
using Genelife.Api.Services;
using Genelife.Components.Family;
using Genelife.Components.Gen;
using Genelife.Components.Housing;

namespace Genelife.UnitTests.Family;

/// <summary>
/// Stable, order-independent capture of the family graph and money supply used by
/// the replay suite. Every list is sorted by network id so query order cannot
/// create a false mismatch.
/// </summary>
public sealed record ReplayCapture(
    SortedDictionary<ulong, string> Traits,
    List<string> Relationships,
    List<string> Pregnancies,
    List<string> Households,
    List<string> Population,
    long MoneyCents,
    long LedgerSourceCents,
    long LedgerSinkCents)
{
    public static ReplayCapture Capture(SimulationEngine engine)
    {
        var world = engine.World ?? throw new InvalidOperationException("World is not running.");
        var economy = engine.Economy ?? throw new InvalidOperationException("Economy is not running.");

        var traits = new SortedDictionary<ulong, string>();
        world.Query(in new QueryDescription().WithAll<Alive, NetworkId>().WithNone<Dead>(),
            (Entity entity, ref Alive alive, ref NetworkId networkId) =>
            {
                var value = world.Has<Traits>(entity)
                    ? world.Get<Traits>(entity).ToString()
                    : "missing";
                traits[networkId.Value] = $"{alive.Age}|{value}";
            });

        var relationships = new List<string>();
        world.Query(in new QueryDescription().WithAll<Relationship, NetworkId>(),
            (Entity _, ref Relationship relationship, ref NetworkId networkId) =>
                relationships.Add(
                    $"{networkId.Value}:{NetworkIdOf(world, relationship.From)}:{relationship.Kind}:{NetworkIdOf(world, relationship.To)}:{relationship.Source}:{relationship.SinceTick}"));
        relationships.Sort(StringComparer.Ordinal);

        var pregnancies = new List<string>();
        world.Query(in new QueryDescription().WithAll<Pregnancy, NetworkId>(),
            (Entity _, ref Pregnancy pregnancy, ref NetworkId networkId) =>
                pregnancies.Add(
                    $"{networkId.Value}:{NetworkIdOf(world, pregnancy.CarryingParent)}:{NetworkIdOf(world, pregnancy.OtherParent)}:{pregnancy.Status}:{pregnancy.Source}:{pregnancy.DueTick}"));
        pregnancies.Sort(StringComparer.Ordinal);

        var households = new List<string>();
        world.Query(in new QueryDescription().WithAll<Household, NetworkId>(),
            (Entity entity, ref Household household, ref NetworkId networkId) =>
                households.Add(
                    $"{networkId.Value}:{NetworkIdOf(world, household.Head)}:{economy.Housing.MemberCount(entity)}:{household.Tenure}"));
        households.Sort(StringComparer.Ordinal);

        var population = new List<string>();
        var snapshot = economy.Life.MetricHistory.Count > 0 ? economy.Life.MetricHistory[^1] : null;
        if (snapshot is not null)
        {
            population.Add($"total={snapshot.Total}");
            population.Add($"births={snapshot.BirthsThisYear}");
            population.Add($"deaths={snapshot.DeathsThisYear}");
            population.Add($"children={snapshot.Children}");
            population.Add($"orphans={snapshot.Orphans}");
            population.Add($"stateCare={snapshot.StateCareChildren}");
            population.Add($"householdSize={snapshot.AverageHouseholdSize}");
            population.Add($"replacement={snapshot.ReplacementRate}");
            population.Add($"trust={snapshot.MinorTrustTotal.Cents}");
            population.Add($"inheritance={snapshot.InheritancePaidThisYear.Cents}");
        }

        return new ReplayCapture(
            traits,
            relationships,
            pregnancies,
            households,
            population,
            economy.TotalMoneySupply().Cents,
            economy.Ledger.SourceTotal.Cents,
            economy.Ledger.SinkTotal.Cents);
    }

    private static ulong NetworkIdOf(World world, Entity entity) =>
        entity != Entity.Null && world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0;
}
