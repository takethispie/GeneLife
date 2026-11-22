using Arch.Core;
using Genelife.Components.Gen;

namespace Genelife.Life;

/// <summary>
/// Shared lifecycle services and pending work. This is the lifecycle analogue of
/// <see cref="Economy.EconomyContext"/>: a plain object owned by the simulation,
/// written only in apply phases, and safe to read from the admin thread.
/// </summary>
public sealed class LifeContext
{
    private readonly List<(Entity Entity, long DestroyTick)> pendingDestroy = new();

    public LifeContext(
        World world,
        SimulationClock? clock = null,
        SimulationRandom? random = null,
        LifeStagePolicy? policy = null,
        CareerPolicy? careers = null,
        PensionPolicy? pensions = null,
        ImmigrationPolicy? immigration = null,
        int randomSeed = 1)
    {
        World = world;
        Clock = clock ?? SimulationClock.Default;
        Random = random ?? new SimulationRandom(randomSeed);
        Policy = policy ?? LifeStagePolicy.Default;
        Careers = careers ?? CareerPolicy.Default;
        Pensions = pensions ?? PensionPolicy.Default;
        Immigration = (immigration ?? ImmigrationPolicy.Default).Clone();
        Policy.Validate();
        Index = new LifecycleIndex();
        ImmigrationState = new ImmigrationState();
    }

    public World World { get; }
    public SimulationClock Clock { get; }
    public SimulationRandom Random { get; }
    public LifeStagePolicy Policy { get; }
    public CareerPolicy Careers { get; }
    public PensionPolicy Pensions { get; }
    public ImmigrationPolicy Immigration { get; }
    public LifecycleIndex Index { get; }
    public ImmigrationState ImmigrationState { get; }

    public List<MortalityRecord> MortalityLog { get; } = new();
    public List<PopulationSnapshot> MetricHistory { get; } = new();
    public List<Entity> DeathQueue { get; } = new();
    public List<EstateRequest> EstateQueue { get; } = new();
    public Dictionary<Entity, long> CriticalSince { get; } = new();
    public bool HasPendingDestroy => pendingDestroy.Count > 0;

    public int GraduatesThisYear { get; set; }
    public int RetiredThisYear { get; set; }
    public int DeathsThisYear { get; set; }
    public int ArrivalsThisYear { get; set; }

    public Func<Entity, NetworkId>? NetworkIdAllocator { get; private set; }

    public void AttachNetworkIdAllocator(Func<Entity, NetworkId> allocator)
    {
        NetworkIdAllocator = allocator;
    }

    public NetworkId AllocateNetworkId(Entity entity)
    {
        var networkId = NetworkIdAllocator?.Invoke(entity) ?? new NetworkId((ulong)entity.Id);
        if (networkId.Value != 0 && !World.Has<NetworkId>(entity))
            World.Add(entity, networkId);
        return networkId;
    }

    public void QueueDestroy(Entity entity, long destroyTick)
    {
        pendingDestroy.Add((entity, destroyTick));
    }

    public void ProcessPendingDestroy(long tick)
    {
        if (pendingDestroy.Count == 0)
            return;

        for (var i = pendingDestroy.Count - 1; i >= 0; i--)
        {
            var (entity, destroyTick) = pendingDestroy[i];
            if (destroyTick > tick)
                continue;

            pendingDestroy.RemoveAt(i);
            if (World.IsAlive(entity))
                World.Destroy(entity);
        }
    }

    public void ResetYearCounters()
    {
        GraduatesThisYear = 0;
        RetiredThisYear = 0;
        DeathsThisYear = 0;
        ArrivalsThisYear = 0;
    }

    public decimal PensionFundingRatio(Values.Money fundBalance)
    {
        var liabilities = 0m;
        var query = new QueryDescription().WithAll<Components.Employment.PensionAccount>().WithNone<Components.Gen.Dead>();
        World.Query(in query, (ref Components.Employment.PensionAccount account) =>
        {
            liabilities += Pensions.ProjectedLiability(account, Policy).Units;
        });

        if (liabilities <= 0m)
            return 1m;

        return Math.Clamp(fundBalance.Units / liabilities, 0m, 1m);
    }

    public void Clear()
    {
        Index.Clear();
        MortalityLog.Clear();
        MetricHistory.Clear();
        DeathQueue.Clear();
        EstateQueue.Clear();
        CriticalSince.Clear();
        pendingDestroy.Clear();
        ImmigrationState.Clear();
        Immigration.TargetPopulation = 0;
        Immigration.Enabled = true;
        ResetYearCounters();
    }
}
