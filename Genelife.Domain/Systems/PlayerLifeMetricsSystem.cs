using Arch.Core;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Player;
using Genelife.Economy;
using Genelife.Messages.Events;
using Genelife.Player;

namespace Genelife.Systems;

/// <summary>
/// Yearly player-life metrics. The report is emitted as an event and the
/// counters reset; no metric mutates the world.
/// </summary>
public sealed class PlayerLifeMetricsSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly PlayerRegistry players;
    private readonly int interval;

    public PlayerLifeMetricsSystem(
        World world,
        EconomyContext economy,
        PlayerRegistry players,
        int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.players = players;
        this.interval = interval ?? economy.Clock.TicksPerYear;
    }

    public string Name => "PlayerLifeMetrics";
    public int Order => 630;
    public Cadence Cadence => new(Math.Max(1, interval));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var total = players.Players.Count;
        var awaiting = 0;
        var employed = 0;
        var housed = 0;

        foreach (var profile in players.All())
        {
            if (profile.AwaitingContinuation)
                awaiting++;

            var sim = profile.ControlledSim;
            if (sim == Entity.Null || !world.IsAlive(sim))
                continue;
            if (world.Has<Employee>(sim))
                employed++;
            if (world.Has<PlayerHousing>(sim) && world.Get<PlayerHousing>(sim).LeaseActive)
                housed++;
        }

        var counters = players.ResetYearCounters();
        economy.Emit(new PlayerLifeMetricsRecorded(
            total,
            awaiting,
            employed,
            housed,
            counters.Continuations,
            counters.Deaths,
            counters.Commands));
    }
}
