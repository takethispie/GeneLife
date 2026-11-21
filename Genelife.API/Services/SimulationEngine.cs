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
using Genelife.Messages.Events;
using Genelife.Systems;
using Wolverine;
using System.Collections.Concurrent;

namespace Genelife.Api.Services;

public class SimulationEngine : IDisposable
{
    private World? world;
    private Metrics metrics = new();
    private EmploymentIndex? employmentIndex;
    private SystemRegistry? systems;
    private bool isRunning;
    private long tick;
    private WorldSnapshot? _latest;
    private readonly object @lock = new();
private readonly NetworkIdRegistry networkIds = new();
    private readonly SnapshotHistory history = new();
    private readonly Action<SystemRunResult> recordSystemRun;
    private readonly IMessageBus? messageBus;
    private readonly ConcurrentQueue<EnqueuedCommand> queuedCommands = new();
    private readonly HashSet<Guid> processedMessageIds = new(1024);
    private readonly WorldSaveService? worldSaveService;
    private readonly IDocumentStore? documentStore;
    private long lastSaveTick;
    private const long AutoSaveInterval = 600;
    private static readonly string[] ActionNames = Enum.GetNames<ActionType>();
    private readonly ConcurrentQueue<EnqueuedCommand> queuedCommands = new();
    private readonly HashSet<Guid> processedMessageIds = new(1024);
    private readonly WorldSaveService? worldSaveService;
    private long lastSaveTick;
    private const long AutoSaveInterval = 600;

    public SimulationEngine(WorldSaveService? worldSaveService = null)
    {
        this.worldSaveService = worldSaveService;
    }

    public SimulationEngine(IMessageBus? messageBus = null)
    {
        this.messageBus = messageBus;
        recordSystemRun = result =>
            metrics.RecordSystem(result.Name, result.Milliseconds, result.AllocatedBytes);
    }

    public bool IsRunning { get { lock (@lock) { return isRunning; } } }
    public World? World { get { lock (@lock) { return world; } } }
    public EmploymentIndex? EmploymentIndex { get { lock (@lock) { return employmentIndex; } } }
    public NetworkIdRegistry NetworkIds => networkIds;
    public SnapshotHistory SnapshotHistory => history;
    public long LastSnapshotBuildTimeMs { get; private set; }
    public double LastTickMilliseconds { get; private set; }
    public long CurrentTick { get { lock (@lock) { return tick; } } }
    public bool SnapshotsEnabled { get; set; } = true;
    public IMessageBus? MessageBus { get; set; }

    public void EnqueueCommand(EnqueuedCommand command)
    {
        queuedCommands.Enqueue(command);
    }

    public int QueuedCommandCount => queuedCommands.Count;

    public void Initialize()
    {
        lock (@lock)
        {
            if (isRunning) return;
            world = World.Create();
            metrics = new Metrics();
            employmentIndex = new EmploymentIndex();

            systems = new SystemRegistry();
            systems.Register(new NeedsDecaySystem(world));
            systems.Register(new DecisionSystem(world));
            systems.Register(new ActionSystem(world));
            systems.Register(new HiringSystem(world, employmentIndex));
            systems.Register(new PayrollSystem(world));
            systems.Register(new JobSeekerSystem(world));

            networkIds.Clear();
            history.Clear();
            tick = 0;
            isRunning = true;
        }
    }

    public void Shutdown()
    {
        World? worldToSave = null;
        EmploymentIndex? indexToSave = null;

        lock (@lock)
        {
            if (!isRunning)
                return;

            worldToSave = world;
            indexToSave = employmentIndex;

            metrics.Flush();
            world?.Dispose();
            world = null;
            employmentIndex = null;
            systems = null;
            history.Clear();
            networkIds.Clear();
            _latest = null;
            isRunning = false;
        }

        if (worldToSave != null && worldSaveService != null && indexToSave != null)
        {
            worldSaveService.SaveAsync(worldToSave, indexToSave).GetAwaiter().GetResult();
        }
    }

    public void Tick(float deltaTime)
    {
        List<object> events = new();

        DrainCommands();

        lock (@lock)
        {
            if (!isRunning || world == null)
                return;

            tick++;

            var currentWorld = world;
            var tickStart = Stopwatch.GetTimestamp();
            var allocatedStart = GC.GetAllocatedBytesForCurrentThread();
            var context = new SimulationContext(tick, deltaTime);

            if (systems != null)
                systems.Update(in context, recordSystemRun);

            CollectSimEvents(currentWorld, events);

if (SnapshotsEnabled)
            {
                var snapshotStart = Stopwatch.GetTimestamp();
                var sims = BuildSimSnapshots(currentWorld);
                var companies = BuildCompanySnapshots(currentWorld, employmentIndex!);
                var next = new WorldSnapshot(tick, WorldSnapshot.CurrentVersion, sims, companies);
                LastSnapshotBuildTimeMs = (long)ElapsedMilliseconds(snapshotStart);

                Interlocked.Exchange(ref _latest, next);
                history.Record(next);

                metrics.RecordSystem("SnapshotBuild", LastSnapshotBuildTimeMs, 0);
            }

            var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedStart;
            LastTickMilliseconds = ElapsedMilliseconds(tickStart);
            metrics.RecordTick(LastTickMilliseconds, allocatedBytes, currentWorld.Size);

            events.Add(new TickCompleted(tick, currentWorld.Size, ElapsedMilliseconds(tickStart)));
        }

        if (MessageBus != null && events.Count > 0)
        {
            foreach (var e in events)
            {
                MessageBus.PublishAsync(e).ConfigureAwait(false).GetAwaiter().GetResult();
            }
        }

        if (worldSaveService != null && employmentIndex != null)
        {
            var currentTick = Interlocked.Read(ref tick);
            if (currentTick - Interlocked.Read(ref lastSaveTick) >= AutoSaveInterval)
            {
                var w = World;
                if (w != null)
                    worldSaveService.SaveAsync(w, employmentIndex).GetAwaiter().GetResult();
                Interlocked.Exchange(ref lastSaveTick, currentTick);
            }
        }
    }

    private void CollectSimEvents(World world, List<object> events)
    {
        var query = new QueryDescription().WithAll<Needs, NetworkId>();
        world.Query(in query, (ref Needs needs, ref NetworkId nid) =>
        {
            events.Add(new SimNeedsChanged(nid.Value, needs.Hunger, needs.Energy, needs.Hygiene, needs.Bladder));
        });
    }

    private void DrainCommands()
    {
        while (queuedCommands.TryDequeue(out var command))
        {
            if (!processedMessageIds.Add(command.MessageId))
                continue;

            lock (@lock)
            {
                if (!isRunning || world == null)
                    continue;

                switch (command.Type)
                {
                    case CommandType.CreateSim:
                        if (command.Payload != null)
                        {
                            var parts = command.Payload.Split('|');
                            if (parts.Length >= 2)
                            {
                                var name = parts[0];
                                var age = int.TryParse(parts[1], out var a) ? a : 18;
                                var entity = world.Create(
                                    new GenName(name),
                                    new Needs(),
                                    new CurrentAction(ActionType.Idle, 0f),
                                    new Alive(age),
                                    new Location(0f, 0f)
                                );
                                var nid = new NetworkId(command.NetworkId.GetValueOrDefault());
                                world.Add(entity, nid);
                            }
                        }
                        break;

                    case CommandType.DeleteSim:
                        if (command.NetworkId.HasValue)
                        {
                            DestroyByNetworkId(world, command.NetworkId.Value);
                        }
                        break;

                    case CommandType.SetAction:
                        if (command.NetworkId.HasValue && command.Payload != null)
                        {
                            SetEntityAction(world, command.NetworkId.Value, command.Payload);
                        }
                        break;

                    case CommandType.CreateCompany:
                        if (command.Payload != null)
                        {
                            var entity = world.Create(new CompanyName(command.Payload));
                            var nid = new NetworkId(command.NetworkId.GetValueOrDefault());
                            world.Add(entity, nid);
                        }
                        break;

                    case CommandType.DeleteCompany:
                        if (command.NetworkId.HasValue)
                        {
                            DestroyByNetworkId(world, command.NetworkId.Value);
                        }
                        break;
                }
            }
        }
    }

    private void DestroyByNetworkId(World world, ulong networkId)
    {
        var query = new QueryDescription().WithAll<NetworkId>();
        world.Query(in query, (Entity entity, ref NetworkId nid) =>
        {
            if (nid.Value == networkId)
                world.Destroy(entity);
        });
    }

    private void SetEntityAction(World world, ulong networkId, string actionType)
    {
        if (!Enum.TryParse<ActionType>(actionType, ignoreCase: true, out var action))
            return;

        var query = new QueryDescription().WithAll<NetworkId>();
        world.Query(in query, (Entity entity, ref NetworkId nid) =>
        {
            if (nid.Value == networkId && world.Has<CurrentAction>(entity))
            {
                ref var ca = ref world.Get<CurrentAction>(entity);
                ca = new CurrentAction(action, 0f);
            }
        });
    }

    public WorldSnapshot? GetLatestSnapshot()
    {
        return Volatile.Read(ref _latest);
    }

    public MetricsSnapshot GetMetricsSnapshot()
    {
        lock (@lock)
        {
            return metrics.Snapshot();
        }
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

            if (networkIds.TryGetEntity(networkId, out entity) && world.IsAlive(entity))
                return true;

            // Fallback for entities created without going through the registry
            // (tests and administrative tooling). Keeps behavior identical to
            // the previous full scan while the hot path uses the map.
            var result = Entity.Null;
            var found = false;
            var query = new QueryDescription().WithAll<NetworkId>();
            world.Query(in query, (Entity e, ref NetworkId nid) =>
            {
                if (!found && nid.Value == networkId.Value)
                {
                    result = e;
                    found = true;
                }
            });

            entity = result;
            return found && world.IsAlive(entity);
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

        world.Query(in query, (Entity entity, ref GenName name, ref Needs needs, ref CurrentAction action) =>
        {
            var status = new SimStatus
            {
                NetworkId = world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : 0,
                Name = name.Name,
                Hunger = MathF.Round(needs.Hunger, 1),
                Energy = MathF.Round(needs.Energy, 1),
                Hygiene = MathF.Round(needs.Hygiene, 1),
                Bladder = MathF.Round(needs.Bladder, 1),
                CurrentAction = GetActionName(action.Type),
                TimeRemaining = MathF.Round(action.TimeRemaining, 2)
            };

            if (world.Has<Location>(entity))
            {
                ref var location = ref world.Get<Location>(entity);
                status.X = MathF.Round(location.Position.X, 2);
                status.Y = MathF.Round(location.Position.Y, 2);
            }

            sims.Add(status);
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
                NetworkId = world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : 0,
                Name = companyName.Name,
                EmployeeCount = index.CountEmployees(entity)
            });
        });

        return companies;
    }

    private static double ElapsedMilliseconds(long startTimestamp)
    {
        return Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
    }

    private static string GetActionName(ActionType action)
    {
        var index = (int)action;
        return (uint)index < (uint)ActionNames.Length ? ActionNames[index] : action.ToString();
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

            var locationQuery = new QueryDescription().WithAll<GenName, Location>();
            world.Query(in locationQuery, (Entity entity, ref Location location) =>
            {
                if (simByEntity.TryGetValue(entity, out var sim))
                {
                    sim.X = location.Position.X;
                    sim.Y = location.Position.Y;
                }
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
                    new Alive(sim.Age),
                    new Location(sim.X, sim.Y)
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