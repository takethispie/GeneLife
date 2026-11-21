using System.Diagnostics;
using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Api.DTOs;
using Genelife.Api.Persistence;
using Genelife.Api.Snapshots;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Enums;
using Genelife.Systems;

namespace Genelife.Api.Services;

public class SimulationEngine : IDisposable
{
    private World? world;
    private Metrics metrics = new();
    private EmploymentIndex? employmentIndex;
    private NeedsDecaySystem? needsDecaySystem;
    private DecisionSystem? decisionSystem;
    private ActionSystem? actionSystem;
    private HiringSystem? hiringSystem;
    private PayrollSystem? payrollSystem;
    private JobSeekerSystem? jobSeekerSystem;
    private bool isRunning;
    private long tick;
    private WorldSnapshot? _latest;
    private readonly object @lock = new();

    public bool IsRunning { get { lock (@lock) { return isRunning; } } }
    public World? World { get { lock (@lock) { return world; } } }
    public EmploymentIndex? EmploymentIndex { get { lock (@lock) { return employmentIndex; } } }
    public long LastSnapshotBuildTimeMs { get; private set; }

    public void Initialize()
    {
        lock (@lock)
        {
            if (isRunning) return;
            world = World.Create();
            metrics = new Metrics();
            employmentIndex = new EmploymentIndex();
            needsDecaySystem = new NeedsDecaySystem(world);
            decisionSystem = new DecisionSystem(world);
            actionSystem = new ActionSystem(world);
            hiringSystem = new HiringSystem(world, employmentIndex);
            payrollSystem = new PayrollSystem(world);
            jobSeekerSystem = new JobSeekerSystem(world);
            tick = 0;
            isRunning = true;
        }
    }

    public void Shutdown()
    {
        lock (@lock)
        {
            if (!isRunning)
                return;

            metrics.Flush();
            world?.Dispose();
            world = null;
            employmentIndex = null;
            _latest = null;
            isRunning = false;
        }
    }

    public void Tick(float deltaTime)
    {
        lock (@lock)
        {
            if (!isRunning || world == null)
                return;

            tick++;

            var currentWorld = world;
            var tickStart = Stopwatch.GetTimestamp();
            var allocatedStart = GC.GetAllocatedBytesForCurrentThread();

            var systemStart = Stopwatch.GetTimestamp();
            needsDecaySystem?.Update(deltaTime);
            metrics.RecordSystem("NeedsDecay", ElapsedMilliseconds(systemStart));

            systemStart = Stopwatch.GetTimestamp();
            decisionSystem?.Update();
            metrics.RecordSystem("Decision", ElapsedMilliseconds(systemStart));

            systemStart = Stopwatch.GetTimestamp();
            actionSystem?.Update(deltaTime);
            metrics.RecordSystem("Action", ElapsedMilliseconds(systemStart));

            systemStart = Stopwatch.GetTimestamp();
            hiringSystem?.Update();
            metrics.RecordSystem("Hiring", ElapsedMilliseconds(systemStart));

            systemStart = Stopwatch.GetTimestamp();
            payrollSystem?.Update();
            metrics.RecordSystem("Payroll", ElapsedMilliseconds(systemStart));

            systemStart = Stopwatch.GetTimestamp();
            jobSeekerSystem?.Update();
            metrics.RecordSystem("JobSeeker", ElapsedMilliseconds(systemStart));

            var snapshotStart = Stopwatch.GetTimestamp();
            var sims = BuildSimSnapshots(currentWorld);
            var companies = BuildCompanySnapshots(currentWorld, employmentIndex!);
            var next = new WorldSnapshot(tick, WorldSnapshot.CurrentVersion, sims, companies);
            LastSnapshotBuildTimeMs = Stopwatch.GetElapsedTime(snapshotStart).Milliseconds;

            Interlocked.Exchange(ref _latest, next);

            metrics.RecordSystem("SnapshotBuild", LastSnapshotBuildTimeMs);

            var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedStart;
            metrics.RecordTick(ElapsedMilliseconds(tickStart), allocatedBytes, currentWorld.Size);
        }
    }

    public WorldSnapshot? GetLatestSnapshot()
    {
        return Volatile.Read(ref _latest);
    }

    public T ExecuteWithWorld<T>(Func<World, T> operation)
    {
        lock (@lock)
        {
            if (world == null)
                throw new InvalidOperationException("Simulation is not running");
            return operation(world);
        }
    }

    public bool TryGetEntity(NetworkId networkId, out Entity entity)
    {
        lock (@lock)
        {
            if (world == null)
            {
                entity = default;
                return false;
            }

            var result = Entity.Null;
            var found = false;
            var query = new QueryDescription().WithAll<NetworkId>();
            world.Query(in query, (Entity e, ref NetworkId nid) =>
            {
                if (nid.Value == networkId.Value)
                {
                    result = e;
                    found = true;
                }
            });

            entity = result;
            return found;
        }
    }

    public bool IsAlive(Entity entity)
    {
        lock (@lock)
        {
            return world != null && world.IsAlive(entity);
        }
    }

    public string? GetOwnerPlayerId(Entity entity)
    {
        lock (@lock)
        {
            if (world == null || !world.IsAlive(entity))
                return null;

            if (world.Has<PlayerControlled>(entity))
            {
                ref var pc = ref world.Get<PlayerControlled>(entity);
                return pc.PlayerId;
            }

            return null;
        }
    }

    public bool SetOwner(Entity entity, string playerId)
    {
        lock (@lock)
        {
            if (world == null || !world.IsAlive(entity))
                return false;

            if (world.Has<PlayerControlled>(entity))
            {
                ref var pc = ref world.Get<PlayerControlled>(entity);
                pc.PlayerId = playerId;
            }
            else
            {
                world.Add(entity, new PlayerControlled(playerId));
            }

            return true;
        }
    }

    public bool RemoveOwner(Entity entity)
    {
        lock (@lock)
        {
            if (world == null || !world.IsAlive(entity) || !world.Has<PlayerControlled>(entity))
                return false;

            world.Remove<PlayerControlled>(entity);
            return true;
        }
    }

    private static List<SimStatus> BuildSimSnapshots(World world)
    {
        var sims = new List<SimStatus>();
        var query = new QueryDescription().WithAll<GenName, Needs, CurrentAction>();

        world.Query(in query, (ref GenName name, ref Needs needs, ref CurrentAction action) =>
        {
            sims.Add(new SimStatus
            {
                NetworkId = 0,
                Name = name.Name,
                Hunger = needs.Hunger,
                Energy = needs.Energy,
                Hygiene = needs.Hygiene,
                Bladder = needs.Bladder,
                CurrentAction = action.Type.ToString(),
                TimeRemaining = action.TimeRemaining
            });
        });

        var netQuery = new QueryDescription().WithAll<GenName, Needs, CurrentAction, NetworkId>();
        world.Query(in netQuery, (ref NetworkId networkId, ref GenName name) =>
        {
            var nameCopy = name.Name;
            var match = sims.Find(s => s.Name == nameCopy);
            if (match != null)
                match.NetworkId = networkId.Value;
        });

        return sims;
    }

    private static List<CompanyStatus> BuildCompanySnapshots(World world, EmploymentIndex index)
    {
        var companies = new List<CompanyStatus>();
        var query = new QueryDescription().WithAll<CompanyName>();

        world.Query(in query, (Entity entity, ref CompanyName companyName) =>
        {
            companies.Add(new CompanyStatus
            {
                NetworkId = 0,
                Name = companyName.Name,
                EmployeeCount = 0
            });
        });

        var netQuery = new QueryDescription().WithAll<CompanyName, NetworkId>();
        world.Query(in netQuery, (Entity entity, ref NetworkId networkId, ref CompanyName companyName) =>
        {
            var nameCopy = companyName.Name;
            var match = companies.Find(c => c.Name == nameCopy);
            if (match != null)
            {
                match.NetworkId = networkId.Value;
                match.EmployeeCount = index.CountEmployees(entity);
            }
        });

        return companies;
    }

    private static double ElapsedMilliseconds(long startTimestamp)
    {
        return Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
    }

    public void Dispose()
    {
        Shutdown();
    }

    public PersistedWorldState CaptureState(NetworkIdRegistry networkIds)
    {
        lock (@lock)
        {
            if (world == null)
                throw new InvalidOperationException("Simulation is not running");

            var currentTick = Volatile.Read(ref tick);
            var sims = new List<PersistedSim>();
            var companies = new List<PersistedCompany>();
            var simByEntity = new Dictionary<Entity, PersistedSim>();

            var simQuery = new QueryDescription().WithAll<GenName, Needs, CurrentAction, Alive, NetworkId>();
            world.Query(in simQuery, (Entity entity, ref GenName name, ref Needs needs, ref CurrentAction action, ref Alive alive, ref NetworkId nid) =>
            {
                var sim = new PersistedSim
                {
                    Name = name.Name,
                    Age = alive.Age,
                    NetworkId = nid.Value,
                    Hunger = needs.Hunger,
                    Energy = needs.Energy,
                    Hygiene = needs.Hygiene,
                    Bladder = needs.Bladder,
                    ActionType = action.Type.ToString(),
                    TimeRemaining = action.TimeRemaining
                };
                sims.Add(sim);
                simByEntity[entity] = sim;
            });

            var jobSeekerQuery = new QueryDescription().WithAll<GenName, JobSeeker>();
            world.Query(in jobSeekerQuery, (Entity entity, ref JobSeeker js) =>
            {
                if (simByEntity.TryGetValue(entity, out var sim))
                    sim.IsLookingForWork = js.IsLookingForWork;
            });

            var playerQuery = new QueryDescription().WithAll<GenName, PlayerControlled>();
            world.Query(in playerQuery, (Entity entity, ref PlayerControlled pc) =>
            {
                if (simByEntity.TryGetValue(entity, out var sim))
                    sim.PlayerId = pc.PlayerId;
            });

            var walletQuery = new QueryDescription().WithAll<GenName, Wallet>();
            world.Query(in walletQuery, (Entity entity, ref Wallet wallet) =>
            {
                if (simByEntity.TryGetValue(entity, out var sim))
                    sim.WalletBalance = wallet.Balance;
            });

            var companyQuery = new QueryDescription().WithAll<CompanyName, NetworkId>();
            world.Query(in companyQuery, (ref CompanyName cname, ref NetworkId nid) =>
            {
                companies.Add(new PersistedCompany
                {
                    Name = cname.Name,
                    NetworkId = nid.Value
                });
            });

            return new PersistedWorldState
            {
                SchemaVersion = PersistedWorldState.CurrentSchemaVersion,
                Tick = currentTick,
                Sims = sims,
                Companies = companies
            };
        }
    }

    public void Restore(PersistedWorldState state, NetworkIdRegistry networkIds)
    {
        lock (@lock)
        {
            if (isRunning)
                throw new InvalidOperationException("Simulation is already running. Stop it before restoring.");

            if (state.SchemaVersion != PersistedWorldState.CurrentSchemaVersion)
                throw new InvalidOperationException(
                    $"Cannot restore schema version {state.SchemaVersion}; expected {PersistedWorldState.CurrentSchemaVersion}");

            Initialize();

            foreach (var sim in state.Sims)
            {
                var entity = world!.Create(
                    new GenName(sim.Name),
                    new Needs { Hunger = sim.Hunger, Energy = sim.Energy, Hygiene = sim.Hygiene, Bladder = sim.Bladder },
                    new CurrentAction(
                        Enum.TryParse<ActionType>(sim.ActionType, out var at) ? at : ActionType.Idle,
                        sim.TimeRemaining),
                    new Alive(sim.Age)
                );

                if (sim.WalletBalance != 0)
                    world.Add(entity, new Wallet(sim.WalletBalance));

                var networkId = new NetworkId(sim.NetworkId);
                world.Add(entity, networkId);
                networkIds.AllocateWithValue(entity, networkId);

                if (sim.IsLookingForWork)
                    world.Add(entity, new JobSeeker(true));

                if (sim.PlayerId != null)
                    world.Add(entity, new PlayerControlled(sim.PlayerId));
            }

            foreach (var company in state.Companies)
            {
                var entity = world!.Create(new CompanyName(company.Name));
                var networkId = new NetworkId(company.NetworkId);
                world.Add(entity, networkId);
                networkIds.AllocateWithValue(entity, networkId);
            }

            tick = state.Tick;
        }
    }
}