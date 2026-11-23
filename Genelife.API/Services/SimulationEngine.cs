using System.Diagnostics;
using System.Collections.Concurrent;
using System.Threading.Channels;
using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Api.DTOs;
using Genelife.Api.Persistence;
using Genelife.Api.Snapshots;
using Genelife.Api.Commands;
using Genelife.Api.Repositories;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Components.Map;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Housing;
using Genelife.Life;
using Genelife.Map;
using Genelife.Messages.Events;
using Genelife.Systems;
using Genelife.Values;
using Marten;
using Wolverine;

namespace Genelife.Api.Services;

public class SimulationEngine : IDisposable
{
    private World? world;
    private Metrics metrics = new();
    private EconomyContext? economy;
    private MapContext? mapContext;
    private MovementIndex? movementIndex;
    private MapMetricsSystem? mapMetrics;
    private readonly ICommandQueue? commandQueue;
    private readonly Dictionary<string, long> lastIntentSequence = new(StringComparer.Ordinal);
    private readonly Dictionary<ulong, BuildingPlaced> emittedMapBuildings = new();
    private int emittedMapVersion;
    private MapSnapshot? cachedMapSnapshot;
    private MapSnapshotCacheKey cachedMapSnapshotKey;
    private SimulationClock clock = SimulationClock.Default;
    private EmploymentIndex? employmentIndex;
    private SystemRegistry? systems;
    private bool isRunning;
    private long tick;
    private WorldSnapshot? _latest;
    private readonly object @lock = new();
private readonly NetworkIdRegistry networkIds = new();
    private readonly SnapshotHistory history = new();
    private readonly Action<SystemRunResult> recordSystemRun;
    private readonly ConcurrentQueue<EnqueuedCommand> queuedCommands = new();
    private readonly ConcurrentQueue<AdminCommand> adminCommands = new();
    private readonly ConcurrentQueue<IntentRejection> intentRejections = new();
    private readonly ConcurrentDictionary<Guid, CommandResult> commandResults = new();
    private readonly ConcurrentDictionary<Guid, byte> seenAdminCommands = new();
    private readonly Channel<object> outboundEvents = Channel.CreateBounded<object>(
        new BoundedChannelOptions(50_000)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = true
        });
    private long droppedEventCount;
    private readonly HashSet<Guid> processedMessageIds = new(1024);
    private readonly IDocumentStore? documentStore;
    private readonly WorldSaveService? worldSaveService;
    private static readonly string[] ActionNames = Enum.GetNames<ActionType>();

    public SimulationEngine(
        WorldSaveService? worldSaveService = null,
        IDocumentStore? documentStore = null,
        ICommandQueue? commandQueue = null)
    {
        this.worldSaveService = worldSaveService;
        this.documentStore = documentStore;
        this.commandQueue = commandQueue;
        recordSystemRun = result =>
            metrics.RecordSystem(result.Name, result.Milliseconds, result.AllocatedBytes);
    }

    public bool IsRunning { get { lock (@lock) { return isRunning; } } }
    public World? World { get { lock (@lock) { return world; } } }
    public EconomyContext? Economy { get { lock (@lock) { return economy; } } }
    public SimulationClock Clock => clock;
    public SimulationClockOptions ClockOptions { get; set; } = new();
    public Genelife.Map.MapOptions MapOptions { get; set; } = Genelife.Map.MapOptions.Default;
    public Genelife.Map.MapGenerationOptions MapGenerationOptions { get; set; } = Genelife.Map.MapGenerationOptions.Default;
    public Genelife.Map.MovementOptions MovementOptions { get; set; } = Genelife.Map.MovementOptions.Default;
    public Genelife.Map.CommuteOptions CommuteOptions { get; set; } = Genelife.Map.CommuteOptions.Default;
    public MapContext? MapContext { get { lock (@lock) { return mapContext; } } }
    public MovementIndex? MovementIndex { get { lock (@lock) { return movementIndex; } } }
    public IReadOnlyList<MapMetricsSnapshot> MapMetricsHistory => mapMetrics?.History ?? Array.Empty<MapMetricsSnapshot>();
    public int MapVersion { get { lock (@lock) { return mapContext?.Version ?? 0; } } }
    public int RandomSeed { get; set; } = 1;
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

    /// <summary>
    /// Events collected by the tick loop for the persistence writer. The tick loop
    /// only enqueues them so document/event-store I/O never blocks the simulation.
    /// </summary>
    public ChannelReader<object> OutboundEvents => outboundEvents.Reader;

    public void Emit(object @event)
    {
        if (!outboundEvents.Writer.TryWrite(@event))
            Interlocked.Increment(ref droppedEventCount);
    }

    public long DroppedEventCount => Interlocked.Read(ref droppedEventCount);

    /// <summary>
    /// Feedback for intents that were admitted by the hub but rejected while the
    /// simulation applied them. The transport drains this queue and sends
    /// <c>IntentRejected</c> to the player's active connections.
    /// </summary>
    public bool TryDequeueIntentRejection(out IntentRejection rejection) =>
        intentRejections.TryDequeue(out rejection!);

    public int PendingIntentRejectionCount => intentRejections.Count;

    private void RejectIntent(string playerId, long sequence, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            return;

        intentRejections.Enqueue(new IntentRejection(playerId, sequence, code, message));
    }

    public void Initialize()
    {
        lock (@lock)
        {
            if (isRunning) return;
            world = World.Create();
            mapContext = MapContext.Generate(world, MapOptions, MapGenerationOptions);
            movementIndex = new MovementIndex();
            mapMetrics = new MapMetricsSystem(world, mapContext.Map);
            emittedMapBuildings.Clear();
            emittedMapVersion = mapContext.Version;
            EmitMapGenerated(0);
            lastIntentSequence.Clear();
            metrics = new Metrics();
            employmentIndex = new EmploymentIndex();
            clock = new SimulationClock(ClockOptions);
            economy = new EconomyContext(world, employmentIndex, clock: clock, randomSeed: RandomSeed);
            economy.Housing.AttachNetworkIdAllocator(entity => networkIds.Allocate(entity));
            economy.Life.AttachNetworkIdAllocator(entity => networkIds.Allocate(entity));

            systems = new SystemRegistry();
            systems.Register(new NeedsDecaySystem(world));
            systems.Register(new PathfindingSystem(world, mapContext.Map, movementIndex, MovementOptions.PathRequestBudgetPerTick));
            systems.Register(new MovementSystem(world, mapContext.Map, movementIndex, MovementOptions.PlayerSpeedTilesPerSecond));
            systems.Register(mapMetrics);
            systems.Register(new HousingModifierSystem(world, economy.Housing));
            systems.Register(new AgingSystem(world, economy));
            systems.Register(new HealthSystem(world, economy));
            systems.Register(new DecisionSystem(world));
            systems.Register(new ActionSystem(world));
            systems.Register(new EducationSystem(world, economy));
            systems.Register(new EnrollmentSystem(world, economy));
            systems.Register(new WorkScheduleSystem(world, economy, mapContext, movementIndex, CommuteOptions));
            systems.Register(new AttendanceSystem(world, economy));
            systems.Register(new CareerSystem(world, economy));
            systems.Register(new StageProgressionSystem(world, economy));
            systems.Register(new HiringSystem(world, employmentIndex, 25, economy));
            systems.Register(new PensionSystem(world, economy));
            systems.Register(new UnemploymentBenefitSystem(world, economy));
            systems.Register(new ElderDeclineSystem(world, economy));
            systems.Register(new MortalitySystem(world, economy));
            systems.Register(new NeedsDeathSystem(world, economy));
            systems.Register(new DeathProcessingSystem(world, economy));
            systems.Register(new EstateSettlementSystem(world, economy));
            systems.Register(new PopulationInflowSystem(world, economy, mapContext));
            systems.Register(new PopulationMetricsSystem(world, economy));
            systems.Register(new CompanyRevenueSystem(world, economy));
            systems.Register(new ConsumerSpendingSystem(world, economy));
            systems.Register(new PayrollSystem(world, economy));
            systems.Register(new CompanyCostsSystem(world, economy));
            systems.Register(new RentSystem(world, economy));
            systems.Register(new HousingMoveSystem(world, economy));
            systems.Register(new HousingMarketSystem(world, economy));
            systems.Register(new MaintenanceSystem(world, economy));
            systems.Register(new EvictionSystem(world, economy));
            systems.Register(new BasicIncomeSystem(world, economy));
            systems.Register(new BankingSystem(world, economy));
            systems.Register(new CorporateTaxSystem(world, economy));
            systems.Register(new CreditSystem(world, economy));
            systems.Register(new CompanySolvencySystem(world, economy));
            systems.Register(new JobSeekerSystem(world, 100, economy));
            systems.Register(new HousingDevelopmentSystem(world, economy, mapContext));
            systems.Register(new EconomyMetricsSystem(world, economy));
            systems.Register(new HousingMetricsSystem(world, economy));

            networkIds.Clear();
            history.Clear();
            tick = 0;
            isRunning = true;
        }
    }

    public void Shutdown()
    {
        WorldSaveBatch? batchToSave = null;

        lock (@lock)
        {
            if (!isRunning)
                return;

            if (world != null && employmentIndex != null && worldSaveService != null)
                batchToSave = worldSaveService.Build(world, employmentIndex);

            metrics.Flush();
            world?.Dispose();
            world = null;
            economy = null;
            mapContext = null;
            movementIndex = null;
            mapMetrics = null;
            employmentIndex = null;
            systems = null;
            history.Clear();
            networkIds.Clear();
            _latest = null;
            isRunning = false;
        }

        if (batchToSave != null && worldSaveService != null)
        {
            worldSaveService.SaveBatchAsync(batchToSave).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Captures a consistent world save batch under the simulation lock. The caller
    /// persists it off the tick thread.
    /// </summary>
    public WorldSaveBatch? PrepareSave()
    {
        lock (@lock)
        {
            if (!isRunning || world == null || employmentIndex == null || worldSaveService == null)
                return null;

            return worldSaveService.Build(world, employmentIndex);
        }
    }

    public void Tick(float deltaTime)
    {
        List<object> events = new();

        DrainCommands();
        DrainAdminCommands();
        DrainPlayerIntents();

        lock (@lock)
        {
            if (!isRunning || world == null)
                return;

            tick++;

            var currentWorld = world;
            var tickStart = Stopwatch.GetTimestamp();
            var allocatedStart = GC.GetAllocatedBytesForCurrentThread();
            var context = new SimulationContext(tick, deltaTime, clock);
            economy.Tick = tick;

            if (systems != null)
                systems.Update(in context, recordSystemRun);

            if (economy is not null)
            {
                foreach (var e in economy.DrainEvents())
                    events.Add(e);
            }

            CollectSimEvents(currentWorld, events);
            CollectMapEvents(events);

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

        if (documentStore != null || MessageBus != null)
        {
            foreach (var e in events)
            {
                if (!outboundEvents.Writer.TryWrite(e))
                    Interlocked.Increment(ref droppedEventCount);
            }
        }
    }

    private void CollectMapEvents(List<object> events)
    {
        if (mapContext is null || mapContext.Version == emittedMapVersion)
            return;

        var mapId = mapContext.Options.MapId;
        var current = mapContext.Map.Placements;
        foreach (var pair in current.OrderBy(pair => pair.Key.Value))
        {
            var placed = BuildPlacedEvent(pair.Key, pair.Value, tick);
            if (!emittedMapBuildings.TryGetValue(pair.Key.Value, out var previous) || !MapEventsEqual(previous, placed))
                events.Add(placed);
            emittedMapBuildings[pair.Key.Value] = placed;
        }

        foreach (var previous in emittedMapBuildings.Values.ToList())
        {
            if (!current.ContainsKey(new NetworkId(previous.NetworkId)))
            {
                events.Add(new BuildingRemoved(mapId, mapContext.Version, previous.NetworkId, tick));
                emittedMapBuildings.Remove(previous.NetworkId);
            }
        }

        emittedMapVersion = mapContext.Version;
    }

    private BuildingPlaced BuildPlacedEvent(NetworkId networkId, Placement placement, long eventTick)
    {
        var status = BuildMapBuilding(networkId, placement);
        return new BuildingPlaced(
            mapContext!.Options.MapId,
            mapContext.Version,
            networkId.Value,
            status.Kind,
            status.Name,
            status.District,
            status.X,
            status.Y,
            status.Width,
            status.Height,
            status.EntryX,
            status.EntryY,
            eventTick);
    }

    private void EmitMapGenerated(long eventTick)
    {
        if (mapContext is null)
            return;
        var options = mapContext.Options;
        Emit(new MapGenerated(
            options.MapId,
            mapContext.Version,
            options.Seed,
            options.Width,
            options.Height,
            options.TileSize,
            eventTick));
    }

    private void EmitAllMapBuildings(long eventTick)
    {
        if (mapContext is null)
            return;

        emittedMapBuildings.Clear();
        foreach (var pair in mapContext.Map.Placements.OrderBy(pair => pair.Key.Value))
        {
            var placed = BuildPlacedEvent(pair.Key, pair.Value, eventTick);
            Emit(placed);
            emittedMapBuildings[pair.Key.Value] = placed;
        }
        emittedMapVersion = mapContext.Version;
    }

    private static bool MapEventsEqual(BuildingPlaced a, BuildingPlaced b) =>
        a.Kind == b.Kind
        && a.Name == b.Name
        && a.District == b.District
        && a.X == b.X
        && a.Y == b.Y
        && a.Width == b.Width
        && a.Height == b.Height
        && a.EntryX == b.EntryX
        && a.EntryY == b.EntryY;

    private void CollectSimEvents(World world, List<object> events)
    {        var query = new QueryDescription().WithAll<Needs, NetworkId>();
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
                                    new Location(0f, 0f),
                                    new SkillProfile(),
                                    new Health(HealthSystem.InitialForAge(age)),
                                    LifecycleFactory.Create(age, tick, clock, economy.Life.Policy)
                                );
                                var nid = new NetworkId(command.NetworkId.GetValueOrDefault());
                                world.Add(entity, nid);
                                if (mapContext is not null)
                                    MapSpawner.PlaceSim(world, entity, mapContext, nid.Value);
                                economy.Life.Index.OnBorn(economy.Life.Policy.StageFor(age));
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

    public Guid EnqueueAdminCommand(AdminCommand command)
    {
        if (!seenAdminCommands.TryAdd(command.CommandId, 0))
            return command.CommandId;

        adminCommands.Enqueue(command);
        return command.CommandId;
    }

    public CommandResult? GetCommandResult(Guid commandId)
    {
        return commandResults.TryGetValue(commandId, out var result) ? result : null;
    }

    private void DrainPlayerIntents()
    {
        if (commandQueue is null)
            return;

        commandQueue.Drain(intent =>
        {
            lock (@lock)
            {
                if (!isRunning || world is null || mapContext is null || movementIndex is null)
                    return;

                if (intent is MoveIntent move)
                    ApplyMoveIntent(move);
                else if (intent is UseBuildingIntent use)
                    ApplyUseBuildingIntent(use);
            }
        });
    }

    private void ApplyMoveIntent(MoveIntent intent)
    {
        if (world is null || mapContext is null || movementIndex is null)
        {
            RejectIntent(intent.PlayerId, intent.Sequence, "world_not_ready", "The world is not ready yet");
            return;
        }
        if (string.IsNullOrWhiteSpace(intent.PlayerId))
            return;

        if (lastIntentSequence.TryGetValue(intent.PlayerId, out var last) && intent.Sequence > 0 && intent.Sequence <= last)
        {
            RejectIntent(intent.PlayerId, intent.Sequence, "stale_sequence", "Intent sequence is stale");
            return;
        }
        if (intent.Sequence > 0)
            lastIntentSequence[intent.PlayerId] = intent.Sequence;

        var sim = ResolvePlayerSim(intent.PlayerId, intent.SimNetworkId);
        if (sim == Entity.Null || !world.IsAlive(sim) || world.Has<Dead>(sim))
        {
            RejectIntent(intent.PlayerId, intent.Sequence, "sim_not_found", "Your sim is not available");
            return;
        }

        var owner = world.Has<PlayerControlled>(sim) ? world.Get<PlayerControlled>(sim).PlayerId : null;
        if (!string.Equals(owner, intent.PlayerId, StringComparison.Ordinal))
        {
            RejectIntent(intent.PlayerId, intent.Sequence, "not_owner", "You do not control that sim");
            return;
        }

        var target = intent.TargetTileX.HasValue && intent.TargetTileY.HasValue
            ? new TileCoord(intent.TargetTileX.Value, intent.TargetTileY.Value)
            : mapContext.Options.ToTile(intent.X, intent.Y);

        if (!mapContext.Options.Bounds.Contains(target))
        {
            RejectIntent(intent.PlayerId, intent.Sequence, "out_of_bounds", "That tile is outside the map");
            return;
        }

        if (!mapContext.Map.IsWalkable(target))
        {
            RejectIntent(intent.PlayerId, intent.Sequence, "blocked", "That tile is blocked");
            return;
        }

        var current = world.Has<TilePosition>(sim)
            ? world.Get<TilePosition>(sim).Tile
            : mapContext.Options.ToTile(
                world.Has<Location>(sim) ? world.Get<Location>(sim).Position.X : 0f,
                world.Has<Location>(sim) ? world.Get<Location>(sim).Position.Y : 0f);

        if (!mapContext.Options.Bounds.Contains(current) || !mapContext.Map.IsWalkable(current))
        {
            current = MapSpawner.FindSpawn(mapContext, world.Has<NetworkId>(sim) ? world.Get<NetworkId>(sim).Value : 0);
            MapSpawner.PlaceSimAt(world, sim, mapContext, current);
        }

        if (!world.Has<MovementState>(sim))
            world.Add(sim, new MovementState(MovementOptions.PlayerSpeedTilesPerSecond));

        if (current == target)
        {
            if (world.Has<MoveTarget>(sim))
            {
                var existing = world.Get<MoveTarget>(sim);
                existing.HasTarget = false;
                world.Get<MoveTarget>(sim) = existing;
            }
            movementIndex.Clear(sim);
            return;
        }

        var moveTarget = new MoveTarget(target, tick, movementIndex.PathVersion);
        if (world.Has<MoveTarget>(sim))
            world.Get<MoveTarget>(sim) = moveTarget;
        else
            world.Add(sim, moveTarget);

        movementIndex.RequestPath(sim, current, target, mapContext.Version);
    }

    private void ApplyUseBuildingIntent(UseBuildingIntent intent)
    {
        if (world is null || mapContext is null)
        {
            RejectIntent(intent.PlayerId, intent.Sequence, "world_not_ready", "The world is not ready yet");
            return;
        }
        if (string.IsNullOrWhiteSpace(intent.PlayerId))
            return;

        if (lastIntentSequence.TryGetValue(intent.PlayerId, out var last) && intent.Sequence > 0 && intent.Sequence <= last)
        {
            RejectIntent(intent.PlayerId, intent.Sequence, "stale_sequence", "Intent sequence is stale");
            return;
        }
        if (intent.Sequence > 0)
            lastIntentSequence[intent.PlayerId] = intent.Sequence;

        if (!networkIds.TryGetEntity(new NetworkId(intent.BuildingNetworkId), out var building) ||
            !world.IsAlive(building))
        {
            RejectIntent(intent.PlayerId, intent.Sequence, "building_not_found", "That building no longer exists");
            return;
        }
        if (!world.Has<Placement>(building))
        {
            RejectIntent(intent.PlayerId, intent.Sequence, "building_not_found", "That building has no placement");
            return;
        }

        var sim = ResolvePlayerSim(intent.PlayerId, intent.SimNetworkId);
        if (sim == Entity.Null || !world.IsAlive(sim) || world.Has<Dead>(sim))
        {
            RejectIntent(intent.PlayerId, intent.Sequence, "sim_not_found", "Your sim is not available");
            return;
        }
        var owner = world.Has<PlayerControlled>(sim) ? world.Get<PlayerControlled>(sim).PlayerId : null;
        if (!string.Equals(owner, intent.PlayerId, StringComparison.Ordinal))
        {
            RejectIntent(intent.PlayerId, intent.Sequence, "not_owner", "You do not control that sim");
            return;
        }

        var placement = world.Get<Placement>(building);
        if (world.Get<TilePosition>(sim).Tile.ChebyshevDistanceTo(placement.Entry) > 1)
        {
            RejectIntent(intent.PlayerId, intent.Sequence, "out_of_range", "Walk to the entrance first");
            return;
        }

        ActionType? action = null;
        if (world.Has<Property>(building))
        {
            action = ActionType.Sleeping;
        }
        else if (world.Has<Office>(building))
        {
            if (!world.Has<Employee>(sim) || world.Get<Employee>(sim).Office != building)
            {
                RejectIntent(intent.PlayerId, intent.Sequence, "role_not_allowed", "You cannot use this building");
                return;
            }
            action = ActionType.Working;
        }
        else if (world.Has<School>(building))
        {
            action = ActionType.Studying;
        }

        if (action is null)
        {
            RejectIntent(intent.PlayerId, intent.Sequence, "role_not_allowed", "You cannot use this building");
            return;
        }

        var current = world.Has<CurrentAction>(sim)
            ? world.Get<CurrentAction>(sim)
            : new CurrentAction(ActionType.Idle, 0f);
        current = new CurrentAction(action.Value, action.Value == ActionType.Studying ? 60f : 0f);
        if (world.Has<CurrentAction>(sim))
            world.Get<CurrentAction>(sim) = current;
        else
            world.Add(sim, current);

        if (!world.Has<MoveTarget>(sim))
            return;
        var target = world.Get<MoveTarget>(sim);
        target.HasTarget = false;
        world.Get<MoveTarget>(sim) = target;
        movementIndex?.Clear(sim);
    }

    private Entity ResolvePlayerSim(string playerId, ulong networkId)    {
        if (world is null)
            return Entity.Null;
        if (networkId != 0 && networkIds.TryGetEntity(new NetworkId(networkId), out var entity))
            return entity;

        var found = Entity.Null;
        var query = new QueryDescription().WithAll<PlayerControlled, NetworkId>();
        world.Query(in query, (Entity candidate, ref PlayerControlled controlled, ref NetworkId _) =>
        {
            if (found == Entity.Null
                && string.Equals(controlled.PlayerId, playerId, StringComparison.Ordinal)
                && !world.Has<Dead>(candidate))
                found = candidate;
        });
        return found;
    }

    public ulong GetPlayerSimNetworkId(string playerId)
    {
        lock (@lock)
        {
            if (world is null || string.IsNullOrWhiteSpace(playerId))
                return 0;
            var sim = ResolvePlayerSim(playerId, 0);
            return sim != Entity.Null && world.IsAlive(sim) && world.Has<NetworkId>(sim)
                ? world.Get<NetworkId>(sim).Value
                : 0;
        }
    }

    public TileCoord? GetPlayerTile(string playerId)
    {
        lock (@lock)
        {
            if (world is null || mapContext is null || string.IsNullOrWhiteSpace(playerId))
                return null;

            var sim = ResolvePlayerSim(playerId, 0);
            if (sim == Entity.Null || !world.IsAlive(sim) || world.Has<Dead>(sim))
                return null;

            if (world.Has<TilePosition>(sim))
                return world.Get<TilePosition>(sim).Tile;

            if (world.Has<Location>(sim))
            {
                var location = world.Get<Location>(sim).Position;
                return mapContext.Options.ToTile(location.X, location.Y);
            }

            return null;
        }
    }

    /// <summary>
    /// Creates a player-controlled sim for a new browser player when the world is
    /// running and the player does not already own a living sim. Returns the
    /// player's sim network id, or 0 when no sim could be created.
    /// </summary>
    public ulong EnsurePlayerSim(string playerId)
    {
        lock (@lock)
        {
            if (!isRunning || world is null || string.IsNullOrWhiteSpace(playerId))
                return 0;

            var existing = ResolvePlayerSim(playerId, 0);
            if (existing != Entity.Null && world.IsAlive(existing) && !world.Has<Dead>(existing))
                return world.Has<NetworkId>(existing) ? world.Get<NetworkId>(existing).Value : 0;

            var repository = new SimRepository(world, networkIds, economy, mapContext);
            var name = playerId.Length > 20 ? $"Player-{playerId[..20]}" : $"Player-{playerId}";
            var networkId = repository.Add(name, 25, playerId);
            Emit(new SimCreated(networkId.Value, name, 25));
            return networkId.Value;
        }
    }

    public void EnsurePlayerSpawn(string playerId)
    {
        lock (@lock)
        {
            if (world is null || economy is null || mapContext is null)
                return;

            var sim = ResolvePlayerSim(playerId, 0);
            if (sim == Entity.Null || !world.IsAlive(sim))
                return;

            if (world.Has<TilePosition>(sim) && mapContext.Map.IsWalkable(world.Get<TilePosition>(sim).Tile))
                return;

            var networkId = world.Has<NetworkId>(sim) ? world.Get<NetworkId>(sim).Value : 0;
            PlayerSpawnService.PlaceAt(world, sim, economy.Housing, mapContext, networkId);
            if (!world.Has<MovementState>(sim))
                world.Add(sim, new MovementState(MovementOptions.PlayerSpeedTilesPerSecond));
        }
    }

    private void RebuildMovementRequests()
    {
        if (world is null || mapContext is null || movementIndex is null)
            return;
        movementIndex.RebuildRequests(world, mapContext.Version);
    }

    private void DrainAdminCommands()
    {
        while (adminCommands.TryDequeue(out var command))
        {
            if (commandResults.ContainsKey(command.CommandId))
                continue;

            CommandResult result;
            lock (@lock)
            {
                if (!isRunning || world == null || economy == null)
                {
                    result = new CommandResult(
                        command.CommandId,
                        false,
                        "Simulation is not running.",
                        null,
                        DateTimeOffset.UtcNow);
                }
                else
                {
                    try
                    {
                        var payload = ExecuteAdminCommand(world, command);
                        result = new CommandResult(
                            command.CommandId,
                            true,
                            null,
                            payload,
                            DateTimeOffset.UtcNow);
                    }
                    catch (Exception ex)
                    {
                        result = new CommandResult(
                            command.CommandId,
                            false,
                            ex.Message,
                            null,
                            DateTimeOffset.UtcNow);
                    }
                }
            }

            commandResults[command.CommandId] = result;
            if (commandResults.Count > 1000)
                commandResults.Clear();
        }
    }

    private object? ExecuteAdminCommand(World world, AdminCommand command)
    {
        var tick = this.tick;
        switch (command)
        {
            case PlaceBuildingCommand place:
            {
                if (mapContext is null)
                    throw new InvalidOperationException("Map is not initialized.");
                if (!networkIds.TryGetEntity(new NetworkId(place.BuildingId), out var building) ||
                    !world.IsAlive(building))
                    throw new InvalidOperationException("Building not found.");
                if (world.Has<Placement>(building))
                    throw new InvalidOperationException("Building already has a placement.");

                BuildingKind kind;
                string district;
                if (world.Has<Property>(building))
                {
                    var property = world.Get<Property>(building);
                    kind = property.IsShelter ? BuildingKind.Civic : BuildingKind.Residential;
                    district = property.District;
                }
                else if (world.Has<Office>(building))
                {
                    kind = BuildingKind.Office;
                    district = "Central";
                }
                else if (world.Has<School>(building))
                {
                    kind = BuildingKind.School;
                    district = world.Get<School>(building).District;
                }
                else
                {
                    throw new InvalidOperationException("Entity is not a placeable building.");
                }

                var footprint = new Footprint(place.Width, place.Height);
                var result = mapContext.Placement.TryPlace(
                    building,
                    new NetworkId(place.BuildingId),
                    kind,
                    footprint,
                    district,
                    new TileCoord(place.X, place.Y),
                    new TileCoord(place.EntryX, place.EntryY));
                if (!result.Success)
                    throw new InvalidOperationException($"{result.Error}: {result.Message}");

                return new
                {
                    buildingId = place.BuildingId,
                    placement = new
                    {
                        x = result.Placement.Anchor.X,
                        y = result.Placement.Anchor.Y,
                        width = result.Placement.Footprint.Width,
                        height = result.Placement.Footprint.Height,
                        entryX = result.Placement.Entry.X,
                        entryY = result.Placement.Entry.Y
                    }
                };
            }

            case CreateSchoolCommand createSchool:
            {
                var repository = new SchoolRepository(world, networkIds, economy, mapContext);
                var id = repository.Add(
                    createSchool.Name,
                    createSchool.District,
                    createSchool.Capacity,
                    createSchool.Quality,
                    createSchool.Emphasis,
                    createSchool.MonthlyCost,
                    createSchool.OwnerId ?? 0);
                return new { schoolId = id.Value };
            }

            case UpdateSchoolCommand updateSchool:
            {
                var repository = new SchoolRepository(world, networkIds, economy, mapContext);
                if (!repository.Update(
                        new NetworkId(updateSchool.SchoolId),
                        updateSchool.Name,
                        updateSchool.Capacity,
                        updateSchool.Quality,
                        updateSchool.Emphasis,
                        updateSchool.MonthlyCost))
                    throw new InvalidOperationException("School not found.");
                return new { schoolId = updateSchool.SchoolId };
            }

            case ForceStageCommand force:
            {
                var progression = new StageProgressionSystem(world, economy!);
                if (!progression.ForceStage(force.SimId, force.Stage, tick))
                    throw new InvalidOperationException("Sim not found.");
                progression.Update(tick, clock);
                return new { simId = force.SimId, stage = force.Stage.ToString() };
            }

            case GraduateSimCommand graduate:
            {
                var progression = new StageProgressionSystem(world, economy!);
                if (!progression.ForceStage(graduate.SimId, LifeStage.Work, tick))
                    throw new InvalidOperationException("Sim not found.");
                progression.Update(tick, clock);
                return new { simId = graduate.SimId, stage = LifeStage.Work.ToString() };
            }

            case RetireSimCommand retire:
            {
                var progression = new StageProgressionSystem(world, economy!);
                if (!progression.ForceStage(retire.SimId, LifeStage.Retirement, tick))
                    throw new InvalidOperationException("Sim not found.");
                progression.Update(tick, clock);
                return new { simId = retire.SimId, stage = LifeStage.Retirement.ToString() };
            }

            case GrantCareCommand care:
            {
                if (!networkIds.TryGetEntity(new NetworkId(care.SimId), out var entity) ||
                    !world.IsAlive(entity) || world.Has<Dead>(entity))
                    throw new InvalidOperationException("Sim not found.");

                if (!world.Has<Health>(entity))
                    world.Add(entity, new Health(50f));

                var health = world.Get<Health>(entity);
                var before = health.Value;
                health.Value = Math.Clamp(health.Value + Math.Max(1f, care.HealthGain), 0f, 100f);
                health.LastCareTick = tick;
                world.Get<Health>(entity) = health;

                var cost = Money.FromUnits(25m);
                var shortfall = Money.Zero;
                if (economy.Housing.HouseholdBySim.TryGetValue(entity, out var household))
                {
                    var finance = new HouseholdFinanceService(world, economy);
                    finance.TryDebit(household, cost, LedgerKind.MedicalCare, tick, null,
                        "medical.care", out shortfall);
                }
                else if (world.Has<Wallet>(entity))
                {
                    ref var wallet = ref world.Get<Wallet>(entity);
                    var paid = wallet.Balance.Min(cost);
                    wallet.Balance -= paid;
                    shortfall = cost - paid;
                }
                else
                {
                    shortfall = cost;
                }

                if (shortfall.IsPositive && economy.Government.Balance >= shortfall)
                {
                    economy.Government.Balance -= shortfall;
                    economy.Ledger.Record(Guid.NewGuid(), tick, LedgerKind.MedicalCare,
                        EconomyAccountIds.Government, care.SimId, shortfall, "medical.care");
                }

                economy.Emit(new MedicalCarePurchased(care.SimId, cost.Cents, tick));
                economy.Emit(new HealthRestored(care.SimId, health.Value, tick));
                return new { simId = care.SimId, healthBefore = before, healthAfter = health.Value };
            }

            case GeneratePopulationCommand generate:
            {
                var generator = new PopulationGenerator(world, networkIds, economy!, employmentIndex!, mapContext);
                var result = generator.Generate(
                    generate.TotalSims,
                    generate.Seed,
                    generate.Adults,
                    generate.Children,
                    tick);
                return result;
            }

            case SetPopulationTargetCommand target:
            {
                economy.Life.Immigration.TargetPopulation = Math.Max(0, target.TargetPopulation);
                economy.Life.Immigration.Enabled = target.Enabled;
                return new
                {
                    targetPopulation = economy.Life.Immigration.TargetPopulation,
                    enabled = economy.Life.Immigration.Enabled
                };
            }

            case PromoteSimCommand promote:
            {
                if (!networkIds.TryGetEntity(new NetworkId(promote.SimId), out var entity) ||
                    !world.IsAlive(entity) || !world.Has<Employee>(entity))
                    throw new InvalidOperationException("Employee not found.");

                var employee = world.Get<Employee>(entity);
                if (employee.Seniority >= Seniority.Director)
                    throw new InvalidOperationException("Employee is already at the top band.");
                var from = employee.Seniority;
                employee.Seniority = economy.Life.Careers.Next(from);
                employee.SalaryPerCycle = employee.SalaryPerCycle
                    .Multiply(1m + economy.Life.Careers.PromotionRaise)
                    .Min(economy.Life.Careers.MaximumSalary);
                world.Get<Employee>(entity) = employee;
                if (world.Has<EmploymentRecord>(entity))
                {
                    var record = world.Get<EmploymentRecord>(entity);
                    record.HighPerformanceMonths = 0;
                    world.Get<EmploymentRecord>(entity) = record;
                }
                economy.Emit(new PromotionGranted(
                    promote.SimId,
                    from.ToString(),
                    employee.Seniority.ToString(),
                    employee.SalaryPerCycle.Cents,
                    tick));
                return new
                {
                    simId = promote.SimId,
                    seniority = employee.Seniority.ToString(),
                    salary = employee.SalaryPerCycle.Cents
                };
            }

            case SetBenefitEligibilityCommand eligibility:
            {
                if (!networkIds.TryGetEntity(new NetworkId(eligibility.SimId), out var entity) ||
                    !world.IsAlive(entity))
                    throw new InvalidOperationException("Sim not found.");

                if (world.Has<UnemploymentRecord>(entity))
                {
                    var record = world.Get<UnemploymentRecord>(entity);
                    record.Eligible = eligibility.Eligible;
                    if (!eligibility.Eligible)
                        record.BenefitPerCycle = Money.Zero;
                    world.Get<UnemploymentRecord>(entity) = record;
                }
                else if (eligibility.Eligible)
                {
                    var seeker = world.Has<JobSeeker>(entity) ? world.Get<JobSeeker>(entity) : default;
                    world.Add(entity, new UnemploymentRecord(
                        tick,
                        seeker.DesiredSalary.Multiply(0.4m),
                        6,
                        true));
                }

                return new { simId = eligibility.SimId, eligible = eligibility.Eligible };
            }

            case TopUpPensionFundCommand topUp:
            {
                if (!topUp.Amount.IsPositive)
                    throw new InvalidOperationException("Top-up amount must be positive.");
                var paid = topUp.Amount.Min(economy.Government.Balance);
                if (!paid.IsPositive)
                    throw new InvalidOperationException("Government has insufficient funds.");
                economy.Government.Balance -= paid;
                economy.Pension.Balance += paid;
                economy.Ledger.Record(
                    Guid.NewGuid(),
                    tick,
                    LedgerKind.PensionTopUp,
                    EconomyAccountIds.Government,
                    EconomyAccountIds.PensionFund,
                    paid,
                    "pension.topup");
                economy.Emit(new PensionFundTopUp(
                    paid.Cents,
                    economy.Pension.Balance.Cents,
                    tick));
                return new { amount = paid.Cents, fundBalance = economy.Pension.Balance.Cents };
            }

            case ForceDeathCommand forceDeath:
            {
                if (!networkIds.TryGetEntity(new NetworkId(forceDeath.SimId), out var entity) ||
                    !world.IsAlive(entity))
                    throw new InvalidOperationException("Sim not found.");
                if (world.Has<Dead>(entity))
                    return new { simId = forceDeath.SimId, alreadyDead = true };

                world.Add(entity, new Dead(tick, forceDeath.Cause));
                economy.Life.DeathQueue.Add(entity);
                economy.Life.DeathsThisYear++;
                return new { simId = forceDeath.SimId, cause = forceDeath.Cause.ToString() };
            }

            case AdvanceAgeCommand advanceAge:
            {
                if (!networkIds.TryGetEntity(new NetworkId(advanceAge.SimId), out var entity) ||
                    !world.IsAlive(entity) || !world.Has<Alive>(entity))
                    throw new InvalidOperationException("Sim not found.");

                var years = Math.Max(1, advanceAge.Years);
                var alive = world.Get<Alive>(entity);
                alive.Age = Math.Min(economy.Life.Policy.MaxAge, alive.Age + years);
                world.Get<Alive>(entity) = alive;
                if (world.Has<Lifecycle>(entity))
                {
                    var lifecycle = world.Get<Lifecycle>(entity);
                    lifecycle.AgeTicks = Math.Min(
                        (long)economy.Life.Policy.MaxAge * clock.TicksPerYear,
                        lifecycle.AgeTicks + (long)years * clock.TicksPerYear);
                    lifecycle.LastAgedTick = tick;
                    world.Get<Lifecycle>(entity) = lifecycle;
                }
                else
                {
                    world.Add(entity, LifecycleFactory.Create(alive.Age, tick, clock, economy.Life.Policy));
                }

                new StageProgressionSystem(world, economy).Update(tick, clock);
                return new { simId = advanceAge.SimId, age = world.Get<Alive>(entity).Age };
            }

            case PauseImmigrationCommand pause:
            {
                economy.Life.ImmigrationState.Paused = pause.Paused;
                if (pause.Paused)
                    economy.Emit(new ImmigrationPaused("admin", tick));
                return new { paused = pause.Paused };
            }

            case TriggerImmigrationWaveCommand wave:
            {
                var count = Math.Max(1, wave.Count);
                economy.Life.ImmigrationState.ArrivalBudget += count;
                if (economy.Life.Immigration.TargetPopulation <= 0)
                    economy.Life.Immigration.TargetPopulation =
                        economy.Life.Index.TotalLiving + count;
                var before = economy.Life.ImmigrationState.ArrivalsThisYear;
                new PopulationInflowSystem(world, economy).Update(tick, clock);
                return new
                {
                    requested = count,
                    admitted = economy.Life.ImmigrationState.ArrivalsThisYear - before
                };
            }

            case SetEnrollmentCommand enrollment:
            {
                var repository = new SchoolRepository(world, networkIds, economy, mapContext);
                if (!repository.SetEnrollment(enrollment.SimId, enrollment.SchoolId, enrollment.Enrolled))
                    throw new InvalidOperationException("Enrollment change failed.");
                return new
                {
                    simId = enrollment.SimId,
                    schoolId = enrollment.SchoolId,
                    enrolled = enrollment.Enrolled
                };
            }

            case CreateOfficeCommand create:
            {
                var repository = new OfficeRepository(world, networkIds, economy, mapContext);
                var id = repository.Add(
                    new NetworkId(create.CompanyId),
                    create.Name,
                    create.Capacity,
                    create.OperatingCost,
                    tick);
                return new { officeId = id.Value };
            }

            case UpdateOfficeCommand update:
            {
                var repository = new OfficeRepository(world, networkIds, economy, mapContext);
                if (!repository.Update(
                        new NetworkId(update.OfficeId),
                        update.Name,
                        update.Capacity,
                        update.OperatingCost,
                        tick))
                    throw new InvalidOperationException("Office not found.");
                return new { officeId = update.OfficeId };
            }

            case CloseOfficeCommand close:
            {
                var repository = new OfficeRepository(world, networkIds, economy, mapContext);
                if (!repository.Close(new NetworkId(close.OfficeId)))
                    throw new InvalidOperationException("Office not found.");
                return new { officeId = close.OfficeId };
            }

            case OpenJobPostingCommand open:
            {
                var repository = new JobPostingRepository(world, networkIds, economy);
                var id = repository.Open(
                    new NetworkId(open.CompanyId),
                    new NetworkId(open.OfficeId),
                    open.Position,
                    open.Salary,
                    open.Requirements,
                    open.Seats,
                    tick);
                return new { postingId = id.Value };
            }

            case CloseJobPostingCommand closePosting:
            {
                var repository = new JobPostingRepository(world, networkIds, economy);
                if (!repository.Close(new NetworkId(closePosting.PostingId), tick))
                    throw new InvalidOperationException("Job posting not found.");
                return new { postingId = closePosting.PostingId };
            }

            case UpdateJobPostingSalaryCommand salary:
            {
                var repository = new JobPostingRepository(world, networkIds, economy);
                if (!repository.UpdateSalary(new NetworkId(salary.PostingId), salary.Salary, tick))
                    throw new InvalidOperationException("Job posting not found.");
                return new { postingId = salary.PostingId, salary = salary.Salary.Cents };
            }

            case ResizeJobPostingCommand resize:
            {
                var repository = new JobPostingRepository(world, networkIds, economy);
                if (!repository.Resize(new NetworkId(resize.PostingId), resize.Seats, tick))
                    throw new InvalidOperationException("Job posting not found.");
                return new { postingId = resize.PostingId, seats = resize.Seats };
            }

            case InjectCapitalCommand capital:
            {
                var repository = new CompanyRepository(world, employmentIndex!, networkIds, economy, map: mapContext);
                if (!repository.InjectCapital(new NetworkId(capital.CompanyId), capital.Amount, tick))
                    throw new InvalidOperationException("Company not found.");
                return new { companyId = capital.CompanyId, amount = capital.Amount.Cents };
            }

            case FireEmployeeCommand fire:
            {
                var repository = new EmploymentRepository(world, employmentIndex!, networkIds, economy);
                if (!repository.Fire(fire.SimNetworkId, tick, companyInitiated: true))
                    throw new InvalidOperationException("Employee not found.");
                return new { simId = fire.SimNetworkId };
            }

            case ResignEmployeeCommand resign:
            {
                var repository = new EmploymentRepository(world, employmentIndex!, networkIds, economy);
                if (!repository.Resign(resign.SimNetworkId, tick))
                    throw new InvalidOperationException("Employee not found.");
                return new { simId = resign.SimNetworkId };
            }

            case IssueLoanCommand loanCommand:
            {
                var system = new CreditSystem(world, economy!);
                var loan = system.IssueLoan(
                    ResolveCompany(world, loanCommand.CompanyId),
                    loanCommand.Amount,
                    tick);
                if (loan is null)
                    throw new InvalidOperationException("Company is not eligible for a loan.");
                return new { loanId = loan.LoanId, amount = loan.Principal.Cents };
            }

            case RestructureCompanyCommand restructure:
            {
                var company = ResolveCompany(world, restructure.CompanyId);
                var account = economy!.Accounts.GetOrCreate(company, Money.Zero, tick);
                account.State = CompanySolvency.Insolvent;
                account.StateChangedAtTick = tick;
                economy.Accounts.Set(company, account);
                return new { companyId = restructure.CompanyId, state = account.State.ToString() };
            }

            case LiquidateCompanyCommand liquidate:
            {
                var company = ResolveCompany(world, liquidate.CompanyId);
                var account = economy!.Accounts.GetOrCreate(company, Money.Zero, tick);
                account.State = CompanySolvency.Insolvent;
                account.InsolventCycles = economy.Options.LiquidationGraceCycles;
                account.StateChangedAtTick = tick;
                economy.Accounts.Set(company, account);
                return new { companyId = liquidate.CompanyId, state = account.State.ToString() };
            }

            case CreatePropertyCommand createProperty:
            {
                var repository = new HousingRepository(world, networkIds, economy!);
                var id = repository.CreateProperty(
                    createProperty.OwnerId,
                    createProperty.Name,
                    createProperty.District,
                    createProperty.Units,
                    createProperty.Tier,
                    createProperty.MonthlyRent?.Units,
                    createProperty.Condition,
                    createProperty.Neighbourhood,
                    tick);
                return new { propertyId = id.Value };
            }

            case UpdatePropertyCommand updateProperty:
            {
                var repository = new HousingRepository(world, networkIds, economy!);
                if (!repository.UpdateProperty(
                        new NetworkId(updateProperty.PropertyId),
                        updateProperty.Name,
                        updateProperty.Condition,
                        updateProperty.MonthlyUpkeep,
                        tick))
                    throw new InvalidOperationException("Property not found.");
                return new { propertyId = updateProperty.PropertyId };
            }

            case UpdateHousingUnitCommand updateUnit:
            {
                var repository = new HousingRepository(world, networkIds, economy!);
                if (!repository.UpdateUnit(
                        new NetworkId(updateUnit.UnitId),
                        updateUnit.Tier,
                        updateUnit.Capacity,
                        updateUnit.BaseRent,
                        updateUnit.Condition))
                    throw new InvalidOperationException("Unit not found.");
                return new { unitId = updateUnit.UnitId };
            }

            case RepairPropertyCommand repair:
            {
                var repository = new HousingRepository(world, networkIds, economy!);
                if (!repository.Repair(new NetworkId(repair.PropertyId), repair.Amount, tick))
                    throw new InvalidOperationException("Property not found.");
                return new { propertyId = repair.PropertyId };
            }

            case AssignHousingCommand assign:
            {
                var repository = new HousingRepository(world, networkIds, economy!);
                if (!repository.Assign(assign.HouseholdId, assign.UnitId, tick))
                    throw new InvalidOperationException("Housing assignment failed.");
                return new { householdId = assign.HouseholdId, unitId = assign.UnitId };
            }

            case EvictHouseholdCommand evict:
            {
                var repository = new HousingRepository(world, networkIds, economy!);
                if (!repository.Evict(evict.HouseholdId, evict.Immediate, tick))
                    throw new InvalidOperationException("Eviction failed.");
                return new { householdId = evict.HouseholdId, immediate = evict.Immediate };
            }

            case StartConstructionCommand construction:
            {
                var repository = new HousingRepository(world, networkIds, economy!);
                var project = repository.StartConstruction(
                    construction.DeveloperId,
                    construction.District,
                    construction.Tier,
                    construction.Units,
                    tick);
                return new { projectId = NetworkIdOf(world, project) };
            }

            case CancelConstructionCommand cancel:
            {
                var repository = new HousingRepository(world, networkIds, economy!);
                if (!repository.CancelConstruction(cancel.ProjectId, tick))
                    throw new InvalidOperationException("Construction project not found.");
                return new { projectId = cancel.ProjectId };
            }

            case SetRentIndexCommand rentIndex:
            {
                new HousingRepository(world, networkIds, economy!).SetMarketIndex(rentIndex.Index);
                return new { index = economy!.Housing.Market.Index };
            }

            case SpawnPopulationCommand spawn:
            {
                var plan = new PopulationPlan
                {
                    Adults = spawn.Adults,
                    Children = spawn.Children,
                    ExpectedMonthlyIncome = spawn.ExpectedMonthlyIncome,
                    ShelterBeds = spawn.ShelterBeds,
                    BasicUnits = spawn.BasicUnits,
                    ComfortableUnits = spawn.ComfortableUnits,
                    LuxuryUnits = spawn.LuxuryUnits
                };
                var assigner = new HouseholdAssigner(world, networkIds, economy!);
                return assigner.Spawn(plan, spawn.Seed, tick);
            }

            case CreateHouseholdCommand createHousehold:
            {
                var repository = new HouseholdRepository(world, networkIds, economy!);
                var id = repository.Create(createHousehold.HeadSimId, tick);
                return new { householdId = id.Value };
            }

            case AddHouseholdMemberCommand addMember:
            {
                var repository = new HouseholdRepository(world, networkIds, economy!);
                if (!repository.AddMember(
                        new NetworkId(addMember.HouseholdId),
                        addMember.SimId,
                        addMember.Role,
                        tick))
                    throw new InvalidOperationException("Household not found.");
                return new { householdId = addMember.HouseholdId, simId = addMember.SimId };
            }

            case RemoveHouseholdMemberCommand removeMember:
            {
                var repository = new HouseholdRepository(world, networkIds, economy!);
                if (!repository.RemoveMember(
                        new NetworkId(removeMember.HouseholdId),
                        removeMember.SimId,
                        tick))
                    throw new InvalidOperationException("Household membership not found.");
                return new { householdId = removeMember.HouseholdId, simId = removeMember.SimId };
            }

            case PurchaseHomeCommand purchase:
            {
                var repository = new HousingRepository(world, networkIds, economy!);
                if (!repository.Purchase(purchase.HouseholdId, purchase.UnitId, tick))
                    throw new InvalidOperationException("Purchase failed.");
                return new { householdId = purchase.HouseholdId, unitId = purchase.UnitId };
            }

            case SellHomeCommand sell:
            {
                var repository = new HousingRepository(world, networkIds, economy!);
                if (!repository.Sell(sell.HouseholdId, sell.UnitId, sell.BuyerHouseholdId, tick))
                    throw new InvalidOperationException("Sale failed.");
                return new { householdId = sell.HouseholdId, unitId = sell.UnitId, buyer = sell.BuyerHouseholdId };
            }

            default:
                throw new InvalidOperationException("Unknown admin command.");
        }
    }

    private Entity ResolveCompany(World world, ulong networkId)
    {
        if (!networkIds.TryGetEntity(new NetworkId(networkId), out var entity) ||
            !world.IsAlive(entity) ||
            !world.Has<CompanyName>(entity))
            throw new InvalidOperationException("Company not found.");
        return entity;
    }

    private static ulong NetworkIdOf(World world, Entity entity) =>
        world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0UL;

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

    public MapStatus BuildMapStatus()
    {
        lock (@lock)
        {
            if (mapContext is null)
                return new MapStatus();

            var map = mapContext.Map;
            var options = map.Options;
            var result = new MapStatus
            {
                MapId = options.MapId,
                Version = map.Version,
                Width = options.Width,
                Height = options.Height,
                TileSize = options.TileSize,
                MinX = options.Bounds.MinX,
                MinY = options.Bounds.MinY,
                MaxX = options.Bounds.MaxX,
                MaxY = options.Bounds.MaxY,
                Buildings = map.Placements.Count,
                OccupiedTiles = map.OccupiedTileCount,
                ReservedTiles = map.ReservedTileCount,
                UnplacedBuildings = CountUnplacedBuildings()
            };

            if (world is null)
                return result;

            foreach (var pair in map.Placements.OrderBy(pair => pair.Key.Value))
                result.BuildingList.Add(BuildMapBuilding(pair.Key, pair.Value));

            result.HealthIssues = map.Reconcile(world).Count;
            result.Unplaced = BuildUnplacedBuildings();
            return result;
        }
    }

    public MapSnapshot BuildMapSnapshot()
    {
        MapSnapshotCacheKey key;
        MapOptions options;
        GeneratedMap? layout;
        List<MapBuildingStatus> buildings;
        int version;

        lock (@lock)
        {
            if (mapContext is null)
                return new MapSnapshot();

            options = mapContext.Options;
            layout = mapContext.Map.Layout;
            version = mapContext.Version;
            key = new MapSnapshotCacheKey(
                options.MapId,
                options.Version,
                options.Seed,
                options.Width,
                options.Height,
                options.TileSize,
                version);

            if (cachedMapSnapshot is not null && cachedMapSnapshotKey == key)
                return cachedMapSnapshot;

            buildings = new List<MapBuildingStatus>(mapContext.Map.Placements.Count);
            foreach (var pair in mapContext.Map.Placements.OrderBy(pair => pair.Key.Value))
                buildings.Add(BuildMapBuilding(pair.Key, pair.Value));
        }

        var snapshot = new MapSnapshot
        {
            MapId = options.MapId,
            Version = version,
            Seed = options.Seed,
            Width = options.Width,
            Height = options.Height,
            TileSize = options.TileSize,
            MinX = options.Bounds.MinX,
            MinY = options.Bounds.MinY,
            MaxX = options.Bounds.MaxX,
            MaxY = options.Bounds.MaxY,
            Tiles = layout is null ? new List<MapTileRun>() : BuildMapTileRuns(layout),
            Buildings = buildings
        };

        lock (@lock)
        {
            cachedMapSnapshot = snapshot;
            cachedMapSnapshotKey = key;
        }

        return snapshot;
    }

    private static List<MapTileRun> BuildMapTileRuns(GeneratedMap layout)
    {
        var bounds = layout.Options.Bounds;
        var runs = new List<MapTileRun>(Math.Max(1, layout.Options.Height));

        for (var y = bounds.MinY; y <= bounds.MaxY; y++)
        {
            var runStart = bounds.MinX;
            var runFlags = layout.FlagsAt(new TileCoord(runStart, y));
            for (var x = bounds.MinX + 1; x <= bounds.MaxX; x++)
            {
                var flags = layout.FlagsAt(new TileCoord(x, y));
                if (flags == runFlags)
                    continue;

                runs.Add(new MapTileRun
                {
                    X = runStart,
                    Y = y,
                    Length = x - runStart,
                    Flags = runFlags.ToString()
                });
                runStart = x;
                runFlags = flags;
            }

            runs.Add(new MapTileRun
            {
                X = runStart,
                Y = y,
                Length = bounds.MaxX - runStart + 1,
                Flags = runFlags.ToString()
            });
        }

        return runs;
    }

    private readonly record struct MapSnapshotCacheKey(
        string MapId,
        int LayoutVersion,
        int Seed,
        int Width,
        int Height,
        float TileSize,
        int MapVersion);

    private List<MapUnplacedBuildingStatus> BuildUnplacedBuildings()
    {
        var result = new List<MapUnplacedBuildingStatus>();
        if (world is null)
            return result;

        var properties = new QueryDescription().WithAll<Property, NetworkId>().WithNone<Placement>();
        world.Query(in properties, (ref Property property, ref NetworkId id) =>
        {
            var footprint = BuildingFootprints.ForProperty(property.UnitCount, property.IsShelter);
            result.Add(new MapUnplacedBuildingStatus
            {
                NetworkId = id.Value,
                Kind = property.IsShelter ? BuildingKind.Civic.ToString() : BuildingKind.Residential.ToString(),
                Name = property.Name,
                District = property.District,
                Width = footprint.Width,
                Height = footprint.Height
            });
        });

        var offices = new QueryDescription().WithAll<Office, NetworkId>().WithNone<Placement>();
        world.Query(in offices, (ref Office office, ref NetworkId id) =>
        {
            var footprint = BuildingFootprints.ForOffice(office.Capacity);
            result.Add(new MapUnplacedBuildingStatus
            {
                NetworkId = id.Value,
                Kind = BuildingKind.Office.ToString(),
                Name = office.OfficeName,
                District = "Central",
                Width = footprint.Width,
                Height = footprint.Height
            });
        });

        var schools = new QueryDescription().WithAll<School, NetworkId>().WithNone<Placement>();
        world.Query(in schools, (ref School school, ref NetworkId id) =>
        {
            var footprint = BuildingFootprints.ForSchool(school.Capacity);
            result.Add(new MapUnplacedBuildingStatus
            {
                NetworkId = id.Value,
                Kind = BuildingKind.School.ToString(),
                Name = school.Name,
                District = school.District,
                Width = footprint.Width,
                Height = footprint.Height
            });
        });

        return result.OrderBy(building => building.NetworkId).ToList();
    }

    public MapHealthStatus BuildMapHealth()
    {
        lock (@lock)
        {
            if (mapContext is null || world is null)
                return new MapHealthStatus();

            var issues = mapContext.Map.Reconcile(world);
            return new MapHealthStatus
            {
                Issues = issues.Count,
                Items = issues.Select(issue => new MapIssueStatus
                {
                    NetworkId = issue.NetworkId,
                    Code = issue.Code,
                    Message = issue.Message,
                    X = issue.Tile?.X,
                    Y = issue.Tile?.Y
                }).ToList()
            };
        }
    }

    public MapViewportStatus? BuildMapViewport(int minX, int minY, int maxX, int maxY)
    {
        lock (@lock)
        {
            if (mapContext is null)
                return null;

            var bounds = new MapBounds(minX, minY, maxX, maxY);
            if (!mapContext.Options.Bounds.Contains(bounds) || bounds.Area > 4096)
                return null;

            var map = mapContext.Map;
            var result = new MapViewportStatus
            {
                MinX = minX,
                MinY = minY,
                MaxX = maxX,
                MaxY = maxY
            };

            foreach (var id in map.BuildingsInBounds(bounds).OrderBy(id => id.Value))
            {
                if (map.TryGetPlacement(id, out var placement))
                    result.Buildings.Add(BuildMapBuilding(id, placement));
            }

            if (world is not null)
            {
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        var flags = map.FlagsAt(new TileCoord(x, y));
                        if (flags == TileFlags.None)
                            continue;
                        result.Tiles.Add(new MapTileStatus { X = x, Y = y, Flags = flags.ToString() });
                    }
                }
            }

            return result;
        }
    }

    public MapBuildingStatus? BuildMapBuilding(ulong networkId)
    {
        lock (@lock)
        {
            if (mapContext is null || !mapContext.Map.TryGetPlacement(new NetworkId(networkId), out var placement))
                return null;
            return BuildMapBuilding(new NetworkId(networkId), placement);
        }
    }

    private MapBuildingStatus BuildMapBuilding(NetworkId networkId, Placement placement)
    {
        var status = new MapBuildingStatus
        {
            NetworkId = networkId.Value,
            Kind = placement.Kind.ToString(),
            Name = placement.Kind.ToString(),
            District = string.Empty,
            X = placement.Anchor.X,
            Y = placement.Anchor.Y,
            Width = placement.Footprint.Width,
            Height = placement.Footprint.Height,
            EntryX = placement.Entry.X,
            EntryY = placement.Entry.Y
        };

        if (world is null || networkIds.TryGetEntity(networkId, out var entity) is false || !world.IsAlive(entity))
            return status;

        if (world.Has<Property>(entity))
        {
            var property = world.Get<Property>(entity);
            status.Name = property.Name;
            status.District = property.District;
            if (property.Owner != Entity.Null && world.IsAlive(property.Owner) &&
                networkIds.TryGetNetworkId(property.Owner, out var ownerId))
                status.OwnerNetworkId = ownerId.Value;
        }
        else if (world.Has<Office>(entity))
        {
            var office = world.Get<Office>(entity);
            status.Name = office.OfficeName;
            status.District = "Central";
            if (world.IsAlive(office.Company) &&
                networkIds.TryGetNetworkId(office.Company, out var companyId))
                status.OwnerNetworkId = companyId.Value;
        }
        else if (world.Has<School>(entity))
        {
            var school = world.Get<School>(entity);
            status.Name = school.Name;
            status.District = school.District;
        }

        return status;
    }

    private int CountUnplacedBuildings()
    {
        if (world is null)
            return 0;

        var count = 0;
        var propertyQuery = new QueryDescription().WithAll<Property>().WithNone<Placement>();
        world.Query(in propertyQuery, (ref Property _) => count++);
        var officeQuery = new QueryDescription().WithAll<Office>().WithNone<Placement>();
        world.Query(in officeQuery, (ref Office _) => count++);
        var schoolQuery = new QueryDescription().WithAll<School>().WithNone<Placement>();
        world.Query(in schoolQuery, (ref School _) => count++);
        return count;
    }

    private MapSummaryStatus? BuildMapSummary()
    {
        var status = BuildMapStatus();
        if (mapContext is null)
            return null;
        return new MapSummaryStatus
        {
            MapId = status.MapId,
            Version = status.Version,
            Buildings = status.Buildings,
            OccupiedTiles = status.OccupiedTiles,
            UnplacedBuildings = status.UnplacedBuildings,
            HealthIssues = status.HealthIssues
        };
    }

    public WorldDataStatus GetWorldData()
    {
        WorldSnapshot? snapshot;
        List<SimStatus>? simsFallback = null;
        List<CompanyStatus>? companiesFallback = null;
        var offices = new List<OfficeRecord>();
        var employees = new List<EmployeeRecord>();
        var postings = new List<PostingRecord>();
        var openSeatPostings = new List<Entity>();
        var applications = new List<ApplicationRecord>();
        var employeeCountsByCompany = new Dictionary<ulong, int>();
        long currentTick;
        bool running;

        lock (@lock)
        {
            running = isRunning;
            currentTick = tick;

            if (!running || world == null)
            {
                return new WorldDataStatus
                {
                    Tick = currentTick,
                    Year = clock.YearOf(currentTick),
                    Month = clock.MonthOfYear(currentTick),
                    IsRunning = running,
                    CapturedAt = DateTimeOffset.UtcNow,
                    Map = BuildMapSummary()
                };
            }

            var currentWorld = world;
            snapshot = _latest;
            if (snapshot is null)
            {
                simsFallback = BuildSimSnapshots(currentWorld);
                companiesFallback = BuildCompanySnapshots(currentWorld, employmentIndex ?? new EmploymentIndex());
            }

            var officeQuery = new QueryDescription().WithAll<Office>();
            currentWorld.Query(in officeQuery, (Entity entity, ref Office office) =>
            {
                offices.Add(new OfficeRecord(
                    entity,
                    GetNetworkId(currentWorld, entity),
                    office.OfficeName,
                    GetNetworkId(currentWorld, office.Company),
                    office.Capacity,
                    office.SeatsUsed,
                    office.OperatingCostPerCycle,
                    office.IsClosed));
            });

            var employeeQuery = new QueryDescription().WithAll<Employee>();
            currentWorld.Query(in employeeQuery, (Entity entity, ref Employee employee) =>
            {
                var companyNetworkId = GetNetworkId(currentWorld, employee.Company);
                employees.Add(new EmployeeRecord(
                    GetNetworkId(currentWorld, entity),
                    companyNetworkId,
                    employee.Office,
                    employee.Position,
                    employee.SalaryPerCycle));

                employeeCountsByCompany.TryGetValue(companyNetworkId, out var count);
                employeeCountsByCompany[companyNetworkId] = count + 1;
            });

            var postingQuery = new QueryDescription().WithAll<JobPosting>();
            currentWorld.Query(in postingQuery, (Entity entity, ref JobPosting posting) =>
            {
                postings.Add(new PostingRecord(
                    entity,
                    GetNetworkId(currentWorld, entity),
                    posting.Position,
                    GetNetworkId(currentWorld, posting.Company),
                    posting.Office,
                    posting.SalaryPerCycle,
                    posting.Requirements.ToString(),
                    posting.OpenSeats,
                    posting.TotalSeats,
                    posting.PostedAtTick,
                    posting.IsClosed));
            });

            var openPositionQuery = new QueryDescription().WithAll<OpenPosition>();
            currentWorld.Query(in openPositionQuery, (ref OpenPosition seat) =>
            {
                openSeatPostings.Add(seat.Posting);
            });

            var applicationQuery = new QueryDescription().WithAll<JobApplication>();
            currentWorld.Query(in applicationQuery, (ref JobApplication application) =>
            {
                applications.Add(new ApplicationRecord(
                    GetNetworkId(currentWorld, application.Applicant),
                    application.JobPosting,
                    GetNetworkId(currentWorld, application.Company),
                    application.Position,
                    application.Status.ToString(),
                    application.MatchScore,
                    application.ApplicationDate));
            });
        }

        var sims = snapshot?.Sims ?? simsFallback ?? new List<SimStatus>();
        var companies = snapshot?.Companies ?? companiesFallback ?? new List<CompanyStatus>();

        var result = new WorldDataStatus
        {
            Tick = currentTick,
            Year = clock.YearOf(currentTick),
            Month = clock.MonthOfYear(currentTick),
            IsRunning = running,
            CapturedAt = DateTimeOffset.UtcNow,
            Sims = sims,
            Map = BuildMapSummary()
        };

        var simNames = new Dictionary<ulong, string>();
        foreach (var sim in sims)
            simNames[sim.NetworkId] = sim.Name;

        var companyNames = new Dictionary<ulong, string>();
        foreach (var company in companies)
            companyNames[company.NetworkId] = company.Name;

        foreach (var company in companies)
        {
            result.Companies.Add(new CompanyStatus
            {
                NetworkId = company.NetworkId,
                Name = company.Name,
                EmployeeCount = employeeCountsByCompany.GetValueOrDefault(company.NetworkId),
                OfficeCount = company.OfficeCount,
                Balance = company.Balance,
                ReservedForPayroll = company.ReservedForPayroll,
                Arrears = company.Arrears,
                RevenueLastCycle = company.RevenueLastCycle,
                WagesLastCycle = company.WagesLastCycle,
                Solvency = company.Solvency
            });
        }

        var officeNames = new Dictionary<Entity, string>();
        foreach (var office in offices)
            officeNames[office.Entity] = office.Name;

        var employeeCountsByOffice = new Dictionary<Entity, int>();
        foreach (var employee in employees)
        {
            employeeCountsByOffice.TryGetValue(employee.Office, out var count);
            employeeCountsByOffice[employee.Office] = count + 1;
        }

        foreach (var office in offices)
        {
            companyNames.TryGetValue(office.CompanyNetworkId, out var companyName);
            result.Offices.Add(new OfficeStatus
            {
                NetworkId = office.NetworkId,
                Name = office.Name,
                CompanyNetworkId = office.CompanyNetworkId,
                Company = companyName ?? string.Empty,
                Capacity = office.Capacity,
                EmployeeCount = employeeCountsByOffice.GetValueOrDefault(office.Entity),
                OpenSeats = Math.Max(0, office.SeatsUsed - employeeCountsByOffice.GetValueOrDefault(office.Entity)),
                SeatsUsed = office.SeatsUsed,
                OperatingCost = office.OperatingCost.Units,
                IsClosed = office.IsClosed
            });
        }

        foreach (var employee in employees)
        {
            simNames.TryGetValue(employee.SimNetworkId, out var simName);
            companyNames.TryGetValue(employee.CompanyNetworkId, out var companyName);

            result.Employees.Add(new EmployeeStatus
            {
                SimNetworkId = employee.SimNetworkId,
                Sim = simName ?? string.Empty,
                CompanyNetworkId = employee.CompanyNetworkId,
                Company = companyName ?? string.Empty,
                Position = employee.Position,
                Salary = employee.Salary.Units
            });
        }

        var postingLookup = new Dictionary<Entity, PostingRecord>();
        foreach (var posting in postings)
            postingLookup[posting.Entity] = posting;

        var seatsByPosting = new Dictionary<Entity, int>();
        foreach (var postingEntity in openSeatPostings)
        {
            seatsByPosting.TryGetValue(postingEntity, out var count);
            seatsByPosting[postingEntity] = count + 1;
        }

        foreach (var postingEntity in openSeatPostings)
        {
            if (!postingLookup.TryGetValue(postingEntity, out var posting))
                continue;

            companyNames.TryGetValue(posting.CompanyNetworkId, out var companyName);
            officeNames.TryGetValue(posting.Office, out var officeName);

            result.OpenPositions.Add(new OpenPositionStatus
            {
                Position = posting.Position,
                Company = companyName ?? string.Empty,
                Office = officeName ?? string.Empty,
                Salary = posting.Salary.Units
            });
        }

        foreach (var posting in postings)
        {
            companyNames.TryGetValue(posting.CompanyNetworkId, out var companyName);
            officeNames.TryGetValue(posting.Office, out var officeName);

            result.JobPostings.Add(new JobPostingStatus
            {
                NetworkId = posting.NetworkId,
                Position = posting.Position,
                CompanyNetworkId = posting.CompanyNetworkId,
                Company = companyName ?? string.Empty,
                Office = officeName ?? string.Empty,
                Salary = posting.Salary.Units,
                Requirements = posting.Requirements,
                OpenPositions = seatsByPosting.GetValueOrDefault(posting.Entity),
                TotalSeats = posting.TotalSeats,
                PostedAtTick = posting.PostedAtTick,
                IsClosed = posting.IsClosed
            });
        }

        foreach (var application in applications)
        {
            simNames.TryGetValue(application.ApplicantNetworkId, out var applicantName);

            var position = application.Position;
            var companyNetworkId = application.CompanyNetworkId;
            if (postingLookup.TryGetValue(application.JobPosting, out var posting))
            {
                if (string.IsNullOrEmpty(position))
                    position = posting.Position;
                if (companyNetworkId == 0)
                    companyNetworkId = posting.CompanyNetworkId;
            }

            companyNames.TryGetValue(companyNetworkId, out var companyName);

            result.JobApplications.Add(new JobApplicationStatus
            {
                Applicant = applicantName ?? string.Empty,
                ApplicantNetworkId = application.ApplicantNetworkId,
                Position = position,
                Company = companyName ?? string.Empty,
                Status = application.Status,
                MatchScore = application.MatchScore,
                ApplicationDate = application.ApplicationDate
            });
        }

        result.Economy = BuildEconomyStatus();
        result.Housing = BuildHousingStatus();
        result.Lifecycle = BuildLifecycleSummary();
        result.RecentLedger = BuildRecentLedger(200);
        return result;
    }

    public EconomyStatus BuildEconomyStatus()
    {
        lock (@lock)
        {
            if (economy is null)
                return new EconomyStatus();

            var latest = economy.MetricHistory.Count > 0 ? economy.MetricHistory[^1] : null;
            var previous = economy.MetricHistory.Count > 1 ? economy.MetricHistory[^2] : null;
            var health = EconomyHealth.Evaluate(latest, previous);
            var supply = economy.GetMoneySupplyBreakdown();

            return new EconomyStatus
            {
                HouseholdCash = supply.HouseholdCash.Units,
                HouseholdSavings = supply.HouseholdSavings.Units,
                CompanyCash = supply.CompanyCash.Units,
                CompanyDebt = economy.Loans.TotalOutstanding().Units,
                PensionFund = supply.Pension.Units,
                GovernmentFund = supply.Government.Units,
                BankFunds = supply.Bank.Units,
                TotalMoneySupply = supply.Total.Units,
                Arrears = economy.TotalArrears().Units,
                WagesPaidThisYear = economy.WagesPaidThisYear.Units,
                RevenueThisYear = economy.RevenueThisYear.Units,
                TaxThisYear = economy.TaxThisYear.Units,
                BasicIncomeThisYear = economy.BasicIncomeThisYear.Units,
                UnemploymentRate = latest?.UnemploymentRate ?? 0m,
                VacancyRate = latest?.VacancyRate ?? 0m,
                WageShare = economy.RevenueThisYear.IsPositive
                    ? economy.WagesPaidThisYear.Cents / (decimal)economy.RevenueThisYear.Cents
                    : 0m,
                Year = clock.YearOf(tick),
                Month = clock.MonthOfYear(tick),
                Health = health.Select(check => new EconomyHealthCheckStatus
                {
                    Name = check.Name,
                    Level = check.Level.ToString(),
                    Detail = check.Detail
                }).ToList()
            };
        }
    }

    public List<LedgerEntryStatus> BuildRecentLedger(
        int take = 200,
        ulong? entityId = null,
        string? kind = null)
    {
        if (economy is null)
            return new List<LedgerEntryStatus>();

        LedgerKind? parsedKind = Enum.TryParse<LedgerKind>(kind, ignoreCase: true, out var parsed)
            ? parsed
            : null;

        return economy.Ledger.Recent(take, entityId, parsedKind)
            .Select(entry => new LedgerEntryStatus
            {
                TransactionId = entry.TransactionId,
                Tick = entry.Tick,
                Kind = entry.Kind.ToString(),
                FromNetworkId = entry.FromNetworkId,
                ToNetworkId = entry.ToNetworkId,
                Amount = entry.Amount.Units,
                Reference = entry.Reference,
                Memo = entry.Memo
            })
            .ToList();
    }

    public FiscalStatus BuildFiscalStatus()
    {
        if (economy is null)
            return new FiscalStatus();

        return new FiscalStatus
        {
            GovernmentBalance = economy.Government.Balance.Units,
            IncomeTaxCollected = economy.Government.IncomeTaxCollected.Units,
            CorporateTaxCollected = economy.Government.CorporateTaxCollected.Units,
            BenefitsPaid = economy.Government.BenefitsPaid.Units,
            OfficeCostsCollected = economy.Government.OfficeCostsCollected.Units,
            PensionBalance = economy.Pension.Balance.Units,
            PensionContributions = economy.Pension.ContributionsThisCycle.Units,
            BankBalance = economy.Bank.Balance.Units,
            BankInterestPool = economy.Bank.InterestPool.Units,
            TotalLoanOutstanding = economy.Loans.TotalOutstanding().Units
        };
    }

    public List<EconomySnapshotStatus> BuildMetricHistory(int years = 20)
    {
        if (economy is null)
            return new List<EconomySnapshotStatus>();

        return economy.MetricHistory
            .TakeLast(Math.Max(1, years))
            .Select(snapshot => new EconomySnapshotStatus
            {
                Tick = snapshot.Tick,
                Year = snapshot.Year,
                HouseholdCash = snapshot.HouseholdCash.Units,
                HouseholdSavings = snapshot.HouseholdSavings.Units,
                CompanyCash = snapshot.CompanyCash.Units,
                CompanyDebt = snapshot.CompanyDebt.Units,
                PensionFund = snapshot.PensionFund.Units,
                GovernmentFund = snapshot.GovernmentFund.Units,
                TotalMoneySupply = snapshot.TotalMoneySupply.Units,
                WagesPaid = snapshot.WagesPaidThisYear.Units,
                Revenue = snapshot.RevenueThisYear.Units,
                Tax = snapshot.TaxThisYear.Units,
                Arrears = snapshot.Arrears.Units,
                UnemploymentRate = snapshot.UnemploymentRate,
                VacancyRate = snapshot.VacancyRate,
                WageShare = snapshot.WageShare,
                Gini = snapshot.Gini,
                BankruptCompanies = snapshot.BankruptCompanies
            })
            .ToList();
    }

    public HousingStatus BuildHousingStatus()
    {
        lock (@lock)
        {
            if (economy is null || world is null)
                return new HousingStatus();

            var currentWorld = world;
            var registry = economy.Housing;
            var properties = 0;
            var units = 0;
            var occupied = 0;
            var vacant = 0;
            var shelterBeds = 0;
            var shelterOccupied = 0;
            var overcrowded = 0;
            var belowCondition = 0;

            currentWorld.Query(in new QueryDescription().WithAll<Property>(), (ref Property _) => properties++);
            currentWorld.Query(in new QueryDescription().WithAll<HousingUnit>(), (ref HousingUnit unit) =>
            {
                units++;
                if (unit.IsVacant)
                    vacant++;
                else
                    occupied++;
                if (unit.Tier == HousingTier.Shelter)
                {
                    shelterBeds++;
                    if (!unit.IsVacant)
                        shelterOccupied++;
                }
                if (unit.IsOvercrowded)
                    overcrowded++;
                if (unit.Condition < 0.2f)
                    belowCondition++;
            });

            var sheltered = 0;
            var homeless = 0;
            var rentArrears = Money.Zero;
            var utilityArrears = Money.Zero;
            var burdens = new List<decimal>();
            var finance = new HouseholdFinanceService(currentWorld, economy);
            currentWorld.Query(in new QueryDescription().WithAll<Household>(), (Entity entity, ref Household household) =>
            {
                if (household.Tenure == Tenure.Sheltered)
                    sheltered++;
                if (household.Unit == Entity.Null)
                    homeless++;
                rentArrears += household.RentArrears;
                utilityArrears += household.UtilityArrears;
                if (household.Tenure == Tenure.Renter)
                {
                    var income = finance.MonthlyIncome(entity);
                    if (income.IsPositive)
                        burdens.Add(household.MonthlyRent.Cents / (decimal)income.Cents);
                }
            });

            var activeProjects = 0;
            var constructionUnits = 0;
            currentWorld.Query(in new QueryDescription().WithAll<ConstructionProject>(), (ref ConstructionProject project) =>
            {
                activeProjects++;
                constructionUnits += project.Units;
            });

            var latest = registry.MetricHistory.Count > 0 ? registry.MetricHistory[^1] : null;
            var health = HousingHealth.Evaluate(latest);

            return new HousingStatus
            {
                Properties = properties,
                Units = units,
                OccupiedUnits = occupied,
                VacantUnits = vacant,
                ShelterBeds = shelterBeds,
                ShelteredHouseholds = sheltered,
                HomelessHouseholds = homeless,
                OvercrowdedUnits = overcrowded,
                UnitsBelowCondition = belowCondition,
                ActiveProjects = activeProjects,
                ConstructionUnits = constructionUnits,
                VacancyRate = units > 0 ? vacant / (decimal)units : 0m,
                RentIndex = registry.Market.Index,
                RentRoll = registry.TotalRentRoll().Units,
                RentCollectedThisMonth = registry.Market.RentCollectedThisMonth.Units,
                UtilityCollectedThisMonth = registry.Market.UtilityCollectedThisMonth.Units,
                UpkeepPaidThisMonth = registry.Market.UpkeepPaidThisMonth.Units,
                RentArrears = rentArrears.Units,
                UtilityArrears = utilityArrears.Units,
                MedianRentBurden = Median(burdens),
                ShelterOccupancy = shelterBeds > 0 ? shelterOccupied / (decimal)shelterBeds : 0m,
                EvictionsThisYear = registry.Market.EvictionsThisYear,
                MovesThisYear = registry.Market.MovesThisYear,
                Year = clock.YearOf(tick),
                Month = clock.MonthOfYear(tick),
                Health = health.Select(check => new HousingHealthCheckStatus
                {
                    Name = check.Name,
                    Level = check.Level.ToString(),
                    Detail = check.Detail
                }).ToList()
            };
        }
    }

    public LifecycleStatus BuildLifecycleStatus()
    {
        lock (@lock)
        {
            var status = new LifecycleStatus();
            if (world is null || economy is null)
                return status;

            var stages = new int[4];
            var ages = new List<int>();
            var healthTotal = 0f;
            var healthCount = 0;

            world.Query(in new QueryDescription().WithAll<Lifecycle, Alive>().WithNone<Dead>(),
                (Entity entity, ref Lifecycle lifecycle, ref Alive alive) =>
                {
                    stages[(int)lifecycle.Stage]++;
                    ages.Add(alive.Age);
                    if (world.Has<Health>(entity))
                    {
                        healthTotal += world.Get<Health>(entity).Value;
                        healthCount++;
                    }
                });

            ages.Sort();
            var total = ages.Count;
            status.TotalSims = total;
            status.Education = stages[(int)LifeStage.Education];
            status.Work = stages[(int)LifeStage.Work];
            status.Retirement = stages[(int)LifeStage.Retirement];
            status.Elderly = stages[(int)LifeStage.Elderly];
            status.AverageAge = total > 0 ? (decimal)ages.Sum() / total : 0m;
            status.MedianAge = Median(ages);
            status.AverageHealth = healthCount > 0 ? (decimal)healthTotal / healthCount : 0m;

            var enrolled = 0;
            var unenrolled = 0;
            world.Query(in new QueryDescription().WithAll<Enrollment>().WithNone<Dead>(),
                (ref Enrollment enrollment) =>
                {
                    if (enrollment.IsUnenrolled)
                        unenrolled++;
                    else
                        enrolled++;
                });
            status.StudentsEnrolled = enrolled;
            status.UnenrolledStudents = unenrolled;
            var schoolAge = total > 0 ? ages.Count(age => age >= economy.Life.Policy.SchoolStartAge &&
                                                          age < economy.Life.Policy.WorkStartAge) : 0;
            status.EnrollmentRate = schoolAge > 0 ? enrolled / (decimal)schoolAge : 1m;

            var employed = 0;
            world.Query(in new QueryDescription().WithAll<Employee>().WithNone<Dead>(),
                (ref Employee _) => employed++);
            var unemployed = 0;
            world.Query(in new QueryDescription().WithAll<Lifecycle, JobSeeker>().WithNone<Employee, Dead>(),
                (ref Lifecycle lifecycle) =>
                {
                    if (lifecycle.Stage == LifeStage.Work)
                        unemployed++;
                });
            status.Employed = employed;
            status.Unemployed = unemployed;
            status.UnemploymentRate = status.Work > 0 ? unemployed / (decimal)status.Work : 0m;

            world.Query(in new QueryDescription().WithAll<School>(), (ref School _) => status.Schools++);

            var pensions = BuildPensions(includeMembers: false);
            status.PensionFund = pensions.FundBalance;
            status.PensionLiabilities = pensions.ProjectedLiabilities;
            status.PensionFundingRatio = pensions.FundingRatio;
            status.AveragePension = pensions.AverageMonthlyPension;
            status.PensionReplacementRate = pensions.ReplacementRate;
            status.MortalityRecords = economy.Life.MortalityLog.Count;
            status.RetiredThisYear = economy.Life.RetiredThisYear;
            status.DeathsThisYear = economy.Life.DeathsThisYear;
            status.ArrivalsThisYear = economy.Life.ArrivalsThisYear;
            status.GraduatesThisYear = economy.Life.GraduatesThisYear;

            var workingAge = status.Work;
            var oldAge = status.Retirement + status.Elderly;
            status.WorkingAgeShare = total > 0 ? workingAge / (decimal)total : 0m;
            status.OldAgeDependencyRatio = workingAge > 0 ? oldAge / (decimal)workingAge : 0m;

            var deaths = economy.Life.MortalityLog.Select(record => record.Age).ToList();
            status.LifeExpectancy = deaths.Count > 0 ? (decimal)deaths.Average() : status.AverageAge;

            var latest = economy.Life.MetricHistory.Count > 0 ? economy.Life.MetricHistory[^1] : null;
            status.LatestSnapshot = latest is null ? null : MapPopulation(latest);

            var policy = economy.Life.Policy;
            status.Policy = new StagePolicyStatus
            {
                SchoolStartAge = policy.SchoolStartAge,
                WorkStartAge = policy.WorkStartAge,
                RetirementAge = policy.RetirementAge,
                ElderlyAge = policy.ElderlyAge,
                MaxAge = policy.MaxAge
            };

            var immigration = economy.Life.Immigration;
            var state = economy.Life.ImmigrationState;
            status.Immigration = new ImmigrationStatus
            {
                TargetPopulation = immigration.TargetPopulation,
                Enabled = immigration.Enabled,
                ArrivalsThisYear = state.ArrivalsThisYear,
                ArrivalBudget = state.ArrivalBudget,
                Pending = state.Pending.Count,
                CancelledThisYear = state.CancelledThisYear,
                Paused = state.Paused
            };

            status.Health = PopulationHealth.Evaluate(latest)
                .Select(check => new LifecycleHealthCheckStatus
                {
                    Name = check.Name,
                    Level = check.Level,
                    Detail = check.Detail
                })
                .ToList();

            return status;
        }
    }

    /// <summary>
    /// Lightweight lifecycle aggregate for the frequently polled world-data
    /// endpoint. It uses the index and the last yearly snapshot instead of
    /// scanning every sim and pension account.
    /// </summary>
    public LifecycleStatus BuildLifecycleSummary()
    {
        lock (@lock)
        {
            var status = new LifecycleStatus();
            if (world is null || economy is null)
                return status;

            var index = economy.Life.Index;
            var latest = economy.Life.MetricHistory.Count > 0 ? economy.Life.MetricHistory[^1] : null;

            status.TotalSims = index.TotalLiving;
            status.Education = index.StageCount(LifeStage.Education);
            status.Work = index.StageCount(LifeStage.Work);
            status.Retirement = index.StageCount(LifeStage.Retirement);
            status.Elderly = index.StageCount(LifeStage.Elderly);
            status.StudentsEnrolled = index.EnrolledStudents;
            status.UnenrolledStudents = index.UnenrolledStudents;

            if (latest is not null)
            {
                status.AverageAge = latest.AverageAge;
                status.MedianAge = latest.MedianAge;
                status.AverageHealth = latest.AverageHealth;
                status.WorkingAgeShare = latest.WorkingAgeShare;
                status.OldAgeDependencyRatio = latest.OldAgeDependencyRatio;
                status.Employed = latest.Employed;
                status.Unemployed = latest.Unemployed;
                status.UnemploymentRate = latest.Work > 0
                    ? latest.Unemployed / (decimal)latest.Work
                    : 0m;
                status.EnrollmentRate = latest.StudentsEnrolled > 0
                    ? latest.StudentsEnrolled / (decimal)Math.Max(1, latest.Education)
                    : 0m;
                status.PensionFundingRatio = latest.PensionFundingRatio;
                status.PensionReplacementRate = latest.PensionReplacementRate;
                status.LifeExpectancy = latest.LifeExpectancy;
            }

            status.PensionFund = economy.Pension.Balance.Units;
            status.GraduatesThisYear = economy.Life.GraduatesThisYear;
            status.RetiredThisYear = economy.Life.RetiredThisYear;
            status.DeathsThisYear = economy.Life.DeathsThisYear;
            status.ArrivalsThisYear = economy.Life.ArrivalsThisYear;
            status.MortalityRecords = economy.Life.MortalityLog.Count;

            world.Query(in new QueryDescription().WithAll<School>(), (ref School _) => status.Schools++);

            var policy = economy.Life.Policy;
            status.Policy = new StagePolicyStatus
            {
                SchoolStartAge = policy.SchoolStartAge,
                WorkStartAge = policy.WorkStartAge,
                RetirementAge = policy.RetirementAge,
                ElderlyAge = policy.ElderlyAge,
                MaxAge = policy.MaxAge
            };

            var immigration = economy.Life.Immigration;
            var state = economy.Life.ImmigrationState;
            status.Immigration = new ImmigrationStatus
            {
                TargetPopulation = immigration.TargetPopulation,
                Enabled = immigration.Enabled,
                ArrivalsThisYear = state.ArrivalsThisYear,
                ArrivalBudget = state.ArrivalBudget,
                Pending = state.Pending.Count,
                CancelledThisYear = state.CancelledThisYear,
                Paused = state.Paused
            };

            status.Health = PopulationHealth.Evaluate(latest)
                .Select(check => new LifecycleHealthCheckStatus
                {
                    Name = check.Name,
                    Level = check.Level,
                    Detail = check.Detail
                })
                .ToList();

            return status;
        }
    }

    public List<SimLifecycleStatus> BuildLifecycleSims(int take = 500, int skip = 0, string? stageFilter = null)
    {
        lock (@lock)
        {
            var result = new List<SimLifecycleStatus>();
            if (world is null || economy is null)
                return result;

            LifeStage? filter = Enum.TryParse<LifeStage>(stageFilter, ignoreCase: true, out var parsed)
                ? parsed
                : null;

            world.Query(in new QueryDescription().WithAll<GenName, Alive, NetworkId>(),
                (Entity entity, ref GenName name, ref Alive alive, ref NetworkId nid) =>
                {
                    var stage = world.Has<Lifecycle>(entity)
                        ? world.Get<Lifecycle>(entity).Stage
                        : economy.Life.Policy.StageFor(alive.Age);
                    if (filter is { } required && stage != required)
                        return;
                    var row = new SimLifecycleStatus
                    {
                        NetworkId = nid.Value,
                        Name = name.Name,
                        Age = alive.Age,
                        Stage = stage.ToString(),
                        Health = world.Has<Health>(entity) ? (decimal)world.Get<Health>(entity).Value : 0m,
                        IsDead = world.Has<Dead>(entity)
                    };

                    if (world.Has<Lifecycle>(entity))
                    {
                        var lifecycle = world.Get<Lifecycle>(entity);
                        row.AgeTicks = lifecycle.AgeTicks;
                        row.StageEnteredTick = lifecycle.StageEnteredTick;
                        row.LastTransitionCause = lifecycle.LastCause.ToString();
                    }

                    if (world.Has<SkillProfile>(entity))
                    {
                        var profile = world.Get<SkillProfile>(entity);
                        row.SkillTotal = (decimal)profile.Total;
                        row.TopSkill = TopSkill(profile);
                    }

                    if (world.Has<Enrollment>(entity))
                    {
                        var enrollment = world.Get<Enrollment>(entity);
                        row.IsUnenrolled = enrollment.IsUnenrolled;
                        row.IsEnrolled = !enrollment.IsUnenrolled;
                        row.Attendance = (decimal)enrollment.Attendance;
                        row.MonthsAttended = enrollment.MonthsAttended;
                        if (world.IsAlive(enrollment.School) && world.Has<School>(enrollment.School))
                        {
                            row.School = world.Get<School>(enrollment.School).Name;
                            row.SchoolNetworkId = GetNetworkId(world, enrollment.School);
                        }
                    }

                    if (world.Has<Employee>(entity))
                    {
                        var employee = world.Get<Employee>(entity);
                        row.IsEmployed = true;
                        row.Position = employee.Position;
                        row.Seniority = employee.Seniority.ToString();
                        row.Salary = employee.SalaryPerCycle.Units;
                        if (world.IsAlive(employee.Company) && world.Has<CompanyName>(employee.Company))
                            row.Employer = world.Get<CompanyName>(employee.Company).Name;
                    }

                    if (world.Has<EmploymentRecord>(entity))
                    {
                        var record = world.Get<EmploymentRecord>(entity);
                        row.Performance = (decimal)record.Performance;
                        row.Warnings = record.Warnings;
                    }

                    if (world.Has<WorkSchedule>(entity))
                        row.IsAtWork = world.Get<WorkSchedule>(entity).IsAtWork;

                    row.IsLookingForWork = world.Has<JobSeeker>(entity);
                    if (world.Has<RetirementIncome>(entity))
                    {
                        var income = world.Get<RetirementIncome>(entity);
                        row.MonthlyPension = income.MonthlyPension.Units;
                        row.RetirementFund = income.FundReference;
                        row.PensionReduced = income.Reduced;
                    }

                    if (world.Has<UnemploymentRecord>(entity))
                        row.UnemploymentCycle = world.Get<UnemploymentRecord>(entity).CyclesClaimed;

                    if (economy.Housing.HouseholdBySim.TryGetValue(entity, out var household))
                        row.HouseholdNetworkId = GetNetworkId(world, household);

                    result.Add(row);
                });

            return result
                .OrderBy(row => row.Stage)
                .ThenBy(row => row.NetworkId)
                .Skip(Math.Max(0, skip))
                .Take(Math.Clamp(take, 1, 2_000))
                .ToList();
        }
    }

    public List<SchoolStatus> BuildSchools()
    {
        lock (@lock)
        {
            var result = new List<SchoolStatus>();
            if (world is null)
                return result;

            var attendance = new Dictionary<Entity, (float Total, int Count)>();
            world.Query(in new QueryDescription().WithAll<Enrollment>().WithNone<Dead>(),
                (ref Enrollment enrollment) =>
                {
                    if (enrollment.IsUnenrolled)
                        return;
                    attendance.TryGetValue(enrollment.School, out var stat);
                    attendance[enrollment.School] = (stat.Total + enrollment.Attendance, stat.Count + 1);
                });

            world.Query(in new QueryDescription().WithAll<School, NetworkId>(),
                (Entity entity, ref School school, ref NetworkId nid) =>
                {
                    var owner = string.Empty;
                    if (world.IsAlive(school.Owner) && world.Has<CompanyName>(school.Owner))
                        owner = world.Get<CompanyName>(school.Owner).Name;

                    var average = attendance.TryGetValue(entity, out var stat) && stat.Count > 0
                        ? stat.Total / stat.Count
                        : 0f;

                    result.Add(new SchoolStatus
                    {
                        NetworkId = nid.Value,
                        Name = school.Name,
                        District = school.District,
                        Capacity = school.Capacity,
                        Enrolled = school.Enrolled,
                        Attendance = (decimal)average,
                        Quality = (decimal)school.Quality,
                        Emphasis = school.Emphasis.ToString(),
                        MonthlyCost = school.MonthlyCost.Units,
                        OwnerNetworkId = school.Owner == Entity.Null ? null : GetNetworkId(world, school.Owner),
                        Owner = owner
                    });
                });

            return result.OrderBy(row => row.Name).ToList();
        }
    }

    public PensionStatus BuildPensions(bool includeMembers = true)
    {
        lock (@lock)
        {
            var status = new PensionStatus();
            if (world is null || economy is null)
                return status;

            status.FundBalance = economy.Pension.Balance.Units;
            status.PaidThisCycle = economy.Pension.PayoutsThisCycle.Units;
            status.ShortfallThisCycle = economy.Pension.Shortfall.Units;
            status.FundingRatio = economy.Life.PensionFundingRatio(economy.Pension.Balance);

            var pensionTotal = Money.Zero;
            var pensionCount = 0;
            var reduced = 0;

            world.Query(in new QueryDescription().WithAll<GenName, Alive, NetworkId, PensionAccount>().WithNone<Dead>(),
                (Entity entity, ref GenName name, ref Alive alive, ref NetworkId nid,
                    ref PensionAccount account) =>
                {
                    var contributions = account.EmployeeContributions + account.EmployerContributions;
                    status.Members++;
                    status.TotalContributions += contributions.Units;
                    status.ProjectedLiabilities += economy.Life.Pensions
                        .ProjectedLiability(account, economy.Life.Policy).Units;

                    if (world.Has<RetirementIncome>(entity))
                    {
                        var income = world.Get<RetirementIncome>(entity);
                        pensionTotal += income.MonthlyPension;
                        pensionCount++;
                        if (income.Reduced)
                            reduced++;
                        if (includeMembers)
                        {
                            status.MemberDetail.Add(new PensionMemberStatus
                            {
                                NetworkId = nid.Value,
                                Name = name.Name,
                                Age = alive.Age,
                                MonthlyPension = income.MonthlyPension.Units,
                                Contributions = contributions.Units,
                                ContributingCycles = account.ContributingCycles,
                                MeansTested = income.MeansTested,
                                Reduced = income.Reduced
                            });
                        }
                    }
                });

            status.AverageMonthlyPension = pensionCount > 0
                ? pensionTotal.Units / pensionCount
                : 0m;
            var monthsPerCycle = Math.Max(1, clock.TicksPerPayCycle / clock.TicksPerMonth);
            var monthlyBase = economy.Life.Careers.BaseSalary.Units / monthsPerCycle;
            status.ReplacementRate = monthlyBase > 0 ? status.AverageMonthlyPension / monthlyBase : 0m;
            status.ReducedThisCycle = reduced;
            status.MemberDetail = status.MemberDetail
                .OrderByDescending(member => member.MonthlyPension)
                .ThenBy(member => member.Name)
                .ToList();
            return status;
        }
    }

    public List<MortalityRecordStatus> BuildMortality(int take = 100)
    {
        lock (@lock)
        {
            if (economy is null)
                return new List<MortalityRecordStatus>();

            return economy.Life.MortalityLog
                .OrderByDescending(record => record.Tick)
                .ThenByDescending(record => record.SimNetworkId)
                .Take(Math.Clamp(take, 1, 1000))
                .Select(record => new MortalityRecordStatus
                {
                    Tick = record.Tick,
                    SimNetworkId = record.SimNetworkId,
                    Name = record.Name,
                    Age = record.Age,
                    Cause = record.Cause.ToString(),
                    Wealth = record.Wealth
                })
                .ToList();
        }
    }

    public List<PopulationSnapshotStatus> BuildPopulationHistory(int years = 20)
    {
        lock (@lock)
        {
            if (economy is null)
                return new List<PopulationSnapshotStatus>();

            return economy.Life.MetricHistory
                .TakeLast(Math.Clamp(years, 1, 50))
                .Select(MapPopulation)
                .ToList();
        }
    }

    public LifecycleOverviewResponse BuildLifecycleOverview() => new()
    {
        Status = BuildLifecycleStatus(),
        Schools = BuildSchools(),
        Pensions = BuildPensions(),
        Mortality = BuildMortality(100),
        PopulationHistory = BuildPopulationHistory(20)
    };

    private static PopulationSnapshotStatus MapPopulation(PopulationSnapshot snapshot) => new()
    {
        Tick = snapshot.Tick,
        Year = snapshot.Year,
        Total = snapshot.Total,
        Education = snapshot.Education,
        Work = snapshot.Work,
        Retirement = snapshot.Retirement,
        Elderly = snapshot.Elderly,
        StudentsEnrolled = snapshot.StudentsEnrolled,
        GraduatesThisYear = snapshot.GraduatesThisYear,
        Employed = snapshot.Employed,
        Unemployed = snapshot.Unemployed,
        RetiredThisYear = snapshot.RetiredThisYear,
        DeathsThisYear = snapshot.DeathsThisYear,
        ArrivalsThisYear = snapshot.ArrivalsThisYear,
        AverageAge = snapshot.AverageAge,
        MedianAge = snapshot.MedianAge,
        AverageHealth = snapshot.AverageHealth,
        AverageAgeAtDeath = snapshot.AverageAgeAtDeath,
        LifeExpectancy = snapshot.LifeExpectancy,
        PensionReplacementRate = snapshot.PensionReplacementRate,
        PensionFundingRatio = snapshot.PensionFundingRatio,
        WorkingAgeShare = snapshot.WorkingAgeShare,
        OldAgeDependencyRatio = snapshot.OldAgeDependencyRatio,
        PyramidBuckets = snapshot.PyramidBuckets.ToList(),
        DeathCauses = snapshot.DeathCauses
            .Select(cause => new DeathCauseStatus { Cause = cause.Cause, Count = cause.Count })
            .ToList()
    };

    private static decimal Median(List<int> values)
    {
        if (values.Count == 0)
            return 0m;
        var middle = values.Count / 2;
        return values.Count % 2 == 0
            ? (values[middle - 1] + values[middle]) / 2m
            : values[middle];
    }

    private static string TopSkill(SkillProfile profile)
    {
        var best = string.Empty;
        var value = 0f;
        foreach (var skill in SkillProfile.Families)
        {
            var proficiency = profile.Get(skill);
            if (proficiency > value)
            {
                value = proficiency;
                best = skill.ToString();
            }
        }
        return best;
    }

    public List<HousingPropertyStatus> BuildHousingProperties()
    {
        lock (@lock)
        {
            if (economy is null || world is null)
                return new List<HousingPropertyStatus>();

            var currentWorld = world;
            var registry = economy.Housing;
            var allocator = new HousingAllocator(currentWorld, economy);
            var result = new List<HousingPropertyStatus>();

            var properties = new List<(Entity Entity, Property Property)>();
            currentWorld.Query(in new QueryDescription().WithAll<Property>(),
                (Entity entity, ref Property property) => properties.Add((entity, property)));
            properties.Sort((a, b) => a.Entity.Id.CompareTo(b.Entity.Id));

            foreach (var (entity, property) in properties)
            {
                var status = new HousingPropertyStatus
                {
                    NetworkId = GetNetworkId(currentWorld, entity),
                    Name = property.Name,
                    District = property.District,
                    OwnerNetworkId = NullableId(currentWorld, property.Owner),
                    Owner = OwnerName(currentWorld, property.Owner),
                    OwnerType = OwnerType(currentWorld, property.Owner),
                    UnitCount = property.UnitCount,
                    Condition = property.Condition,
                    Neighbourhood = property.NeighbourhoodScore,
                    MonthlyUpkeep = property.MonthlyUpkeep.Units,
                    IsShelter = property.IsShelter
                };

                if (registry.UnitsByProperty.TryGetValue(entity, out var units))
                {
                    foreach (var unitEntity in units)
                    {
                        if (!currentWorld.IsAlive(unitEntity) || !currentWorld.Has<HousingUnit>(unitEntity))
                            continue;

                        var unit = currentWorld.Get<HousingUnit>(unitEntity);
                        var household = registry.HouseholdIn(unitEntity);
                        var householdId = NullableId(currentWorld, household);
                        status.Units.Add(new HousingUnitStatus
                        {
                            NetworkId = GetNetworkId(currentWorld, unitEntity),
                            UnitNumber = unit.UnitNumber,
                            Tier = unit.Tier.ToString(),
                            Capacity = unit.Capacity,
                            Occupants = registry.MemberCount(household),
                            BaseRent = unit.BaseRent.Units,
                            MarketRent = allocator.MarketRent(unitEntity).Units,
                            PurchasePrice = unit.PurchasePrice.Units,
                            Condition = unit.Condition,
                            IsVacant = unit.IsVacant,
                            IsOvercrowded = unit.IsOvercrowded,
                            IsCondemned = unit.IsCondemned,
                            HouseholdNetworkId = householdId,
                            Household = household == Entity.Null ? string.Empty : HeadName(currentWorld, household)
                        });

                        if (unit.IsVacant)
                            status.VacantUnits++;
                        else
                            status.OccupiedUnits++;
                    }
                }

                status.UnitCount = status.Units.Count;
                result.Add(status);
            }

            return result;
        }
    }

    public List<HouseholdStatus> BuildHouseholds()
    {
        lock (@lock)
        {
            if (economy is null || world is null)
                return new List<HouseholdStatus>();

            var currentWorld = world;
            var registry = economy.Housing;
            var finance = new HouseholdFinanceService(currentWorld, economy);
            var households = new List<(Entity Entity, Household Household)>();
            currentWorld.Query(in new QueryDescription().WithAll<Household>(),
                (Entity entity, ref Household household) => households.Add((entity, household)));
            households.Sort((a, b) => a.Entity.Id.CompareTo(b.Entity.Id));

            var result = new List<HouseholdStatus>();
            foreach (var (entity, household) in households)
            {
                var unit = household.Unit;
                var unitData = unit != Entity.Null && currentWorld.IsAlive(unit) && currentWorld.Has<HousingUnit>(unit)
                    ? currentWorld.Get<HousingUnit>(unit)
                    : default;
                var property = unitData.Property != Entity.Null && currentWorld.IsAlive(unitData.Property) &&
                               currentWorld.Has<Property>(unitData.Property)
                    ? currentWorld.Get<Property>(unitData.Property)
                    : default;

                var members = new List<string>();
                foreach (var member in registry.GetMembers(entity))
                {
                    if (currentWorld.IsAlive(member) && currentWorld.Has<GenName>(member))
                        members.Add(currentWorld.Get<GenName>(member).Name);
                }

                result.Add(new HouseholdStatus
                {
                    NetworkId = GetNetworkId(currentWorld, entity),
                    HeadNetworkId = GetNetworkId(currentWorld, household.Head),
                    Head = Name(currentWorld, household.Head),
                    UnitNetworkId = NullableId(currentWorld, unit),
                    Unit = unitData.UnitNumber ?? string.Empty,
                    District = property.District ?? string.Empty,
                    Property = property.Name ?? string.Empty,
                    Tenure = household.Tenure.ToString(),
                    Members = registry.MemberCount(entity),
                    Earners = finance.Earners(entity),
                    MonthlyRent = household.MonthlyRent.Units,
                    MonthlyIncome = finance.MonthlyIncome(entity).Units,
                    RentArrears = household.RentArrears.Units,
                    UtilityArrears = household.UtilityArrears.Units,
                    Debt = household.Debt.Units,
                    ArrearsCycles = household.ArrearsCycles,
                    NoticeStage = household.NoticeStage,
                    CureDeadlineTick = household.CureDeadlineTick,
                    Overcrowded = unit != Entity.Null && unitData.OccupantCount > unitData.Capacity,
                    NoUtilities = household.NoUtilities,
                    Homeless = unit == Entity.Null,
                    MembersList = members
                });
            }

            return result;
        }
    }

    public List<HousingRentRollStatus> BuildHousingRentRoll()
    {
        lock (@lock)
        {
            if (economy is null || world is null)
                return new List<HousingRentRollStatus>();

            var currentWorld = world;
            var registry = economy.Housing;
            var result = new List<HousingRentRollStatus>();

            foreach (var (owner, units) in registry.UnitsByOwner)
            {
                var status = new HousingRentRollStatus
                {
                    OwnerNetworkId = NullableId(currentWorld, owner),
                    Owner = OwnerName(currentWorld, owner),
                    OwnerType = OwnerType(currentWorld, owner)
                };

                foreach (var unit in units)
                {
                    if (!currentWorld.IsAlive(unit) || !currentWorld.Has<HousingUnit>(unit))
                        continue;
                    status.Units++;
                    var household = registry.HouseholdIn(unit);
                    if (household == Entity.Null || !currentWorld.IsAlive(household) ||
                        !currentWorld.Has<Household>(household))
                        continue;

                    var data = currentWorld.Get<Household>(household);
                    status.OccupiedUnits++;
                    status.RentRoll += data.MonthlyRent.Units;
                    status.RentArrears += data.RentArrears.Units;
                    status.UtilityArrears += data.UtilityArrears.Units;
                }

                result.Add(status);
            }

            result.Sort((a, b) => b.RentRoll.CompareTo(a.RentRoll));
            return result;
        }
    }

    public List<HousingConstructionStatus> BuildHousingConstructionProjects()
    {
        lock (@lock)
        {
            if (economy is null || world is null)
                return new List<HousingConstructionStatus>();

            var currentWorld = world;
            var result = new List<HousingConstructionStatus>();
            currentWorld.Query(in new QueryDescription().WithAll<ConstructionProject>(),
                (Entity entity, ref ConstructionProject project) =>
                {
                    result.Add(new HousingConstructionStatus
                    {
                        NetworkId = GetNetworkId(currentWorld, entity),
                        DeveloperNetworkId = NullableId(currentWorld, project.Developer),
                        Developer = OwnerName(currentWorld, project.Developer),
                        District = project.District,
                        Tier = project.Tier.ToString(),
                        Units = project.Units,
                        MonthsRemaining = project.MonthsRemaining,
                        Cost = project.Cost.Units,
                        PaidToDate = project.PaidToDate.Units
                    });
                });
            return result;
        }
    }

    public List<HousingSnapshotStatus> BuildHousingMetricHistory(int years = 20)
    {
        if (economy is null)
            return new List<HousingSnapshotStatus>();

        return economy.Housing.MetricHistory
            .TakeLast(Math.Max(1, years))
            .Select(snapshot => new HousingSnapshotStatus
            {
                Tick = snapshot.Tick,
                Year = snapshot.Year,
                Properties = snapshot.Properties,
                Units = snapshot.Units,
                OccupiedUnits = snapshot.OccupiedUnits,
                VacantUnits = snapshot.VacantUnits,
                ShelterBeds = snapshot.ShelterBeds,
                ShelteredHouseholds = snapshot.ShelteredHouseholds,
                HomelessHouseholds = snapshot.HomelessHouseholds,
                OvercrowdedUnits = snapshot.OvercrowdedUnits,
                UnitsBelowCondition = snapshot.UnitsBelowCondition,
                VacancyRate = snapshot.VacancyRate,
                RentIndex = snapshot.RentIndex,
                RentRoll = snapshot.RentRoll.Units,
                RentCollected = snapshot.RentCollected.Units,
                RentArrears = snapshot.RentArrears.Units,
                UtilityArrears = snapshot.UtilityArrears.Units,
                UpkeepPaid = snapshot.UpkeepPaid.Units,
                EvictionsThisYear = snapshot.EvictionsThisYear,
                ConstructionUnits = snapshot.ConstructionUnits,
                MedianRentBurden = snapshot.MedianRentBurden,
                ShelterOccupancy = snapshot.ShelterOccupancy
            })
            .ToList();
    }

    private static decimal Median(List<decimal> values)
    {
        if (values.Count == 0)
            return 0m;
        values.Sort();
        var middle = values.Count / 2;
        return values.Count % 2 == 0
            ? (values[middle - 1] + values[middle]) / 2m
            : values[middle];
    }

    private static ulong? NullableId(World world, Entity entity)
    {
        var id = GetNetworkId(world, entity);
        return id == 0 ? null : id;
    }

    private static string Name(World world, Entity entity)
    {
        if (entity == Entity.Null || !world.IsAlive(entity))
            return string.Empty;
        if (world.Has<GenName>(entity))
            return world.Get<GenName>(entity).Name;
        if (world.Has<CompanyName>(entity))
            return world.Get<CompanyName>(entity).Name;
        return string.Empty;
    }

    private static string HeadName(World world, Entity household)
    {
        if (!world.IsAlive(household) || !world.Has<Household>(household))
            return string.Empty;
        return Name(world, world.Get<Household>(household).Head);
    }

    private static string OwnerName(World world, Entity owner)
    {
        if (owner == Entity.Null || !world.IsAlive(owner))
            return "Municipality";
        if (world.Has<CompanyName>(owner))
            return world.Get<CompanyName>(owner).Name;
        if (world.Has<Household>(owner))
            return HeadName(world, owner);
        return "Municipality";
    }

    private static string OwnerType(World world, Entity owner)
    {
        if (owner == Entity.Null || !world.IsAlive(owner))
            return "Municipality";
        if (world.Has<CompanyName>(owner))
            return "Company";
        if (world.Has<Household>(owner))
            return "Household";
        return "Municipality";
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
            var isPlayerControlled = world.Has<PlayerControlled>(entity);
            var status = new SimStatus
            {
                NetworkId = world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : 0,
                Name = name.Name,
                Age = world.TryGet<Alive>(entity, out var alive) ? alive.Age : 0,
                IsPlayerControlled = isPlayerControlled,
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

            if (world.Has<TilePosition>(entity))
            {
                var tile = world.Get<TilePosition>(entity).Tile;
                status.TileX = tile.X;
                status.TileY = tile.Y;
            }

            if (world.Has<MoveTarget>(entity))
            {
                var target = world.Get<MoveTarget>(entity);
                status.HasMoveTarget = target.HasTarget;
                status.MoveTargetX = target.Tile.X;
                status.MoveTargetY = target.Tile.Y;
            }

            if (world.Has<Wallet>(entity))
                status.WalletBalance = world.Get<Wallet>(entity).Balance.Units;
            if (world.Has<SavingsAccount>(entity))
                status.SavingsBalance = world.Get<SavingsAccount>(entity).Balance.Units;
            if (world.Has<ConsumptionState>(entity))
                status.UnmetDemand = world.Get<ConsumptionState>(entity).UnmetDemand.Units;

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
            var officeCount = 0;
            var officeQuery = new QueryDescription().WithAll<Office>();
            world.Query(in officeQuery, (ref Office office) =>
            {
                if (office.Company == entity && !office.IsClosed)
                    officeCount++;
            });

            var status = new CompanyStatus
            {
                NetworkId = world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : 0,
                Name = companyName.Name,
                EmployeeCount = index.CountEmployees(entity),
                OfficeCount = officeCount
            };

            if (world.Has<CompanyAccount>(entity))
            {
                ref var account = ref world.Get<CompanyAccount>(entity);
                status.Balance = account.Balance.Units;
                status.ReservedForPayroll = account.ReservedForPayroll.Units;
                status.Arrears = account.Arrears.Units;
                status.RevenueLastCycle = account.RevenueLastCycle.Units;
                status.WagesLastCycle = account.WagesLastCycle.Units;
                status.Solvency = account.State.ToString();
            }

            companies.Add(status);
        });

        return companies;
    }

    private static ulong GetNetworkId(World world, Entity entity)
    {
        if (entity.Equals(Entity.Null))
            return 0UL;

        return world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0UL;
    }

    private readonly record struct OfficeRecord(
        Entity Entity,
        ulong NetworkId,
        string Name,
        ulong CompanyNetworkId,
        int Capacity,
        int SeatsUsed,
        Money OperatingCost,
        bool IsClosed);

    private readonly record struct EmployeeRecord(
        ulong SimNetworkId,
        ulong CompanyNetworkId,
        Entity Office,
        string Position,
        Money Salary);

    private readonly record struct PostingRecord(
        Entity Entity,
        ulong NetworkId,
        string Position,
        ulong CompanyNetworkId,
        Entity Office,
        Money Salary,
        string Requirements,
        int OpenSeats,
        int TotalSeats,
        long PostedAtTick,
        bool IsClosed);

    private readonly record struct ApplicationRecord(
        ulong ApplicantNetworkId,
        Entity JobPosting,
        ulong CompanyNetworkId,
        string Position,
        string Status,
        float MatchScore,
        DateTime ApplicationDate);

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
            if (world == null || economy == null)
                throw new InvalidOperationException("Simulation is not running");

            var currentTick = Volatile.Read(ref tick);
            var sims = new List<PersistedSim>();
            var companies = new List<PersistedCompany>();
            var offices = new List<PersistedOffice>();
            var postings = new List<PersistedPosting>();
            var applications = new List<PersistedApplication>();
            var loans = new List<PersistedLoan>();
            var pendingRevenue = new List<PersistedPendingRevenue>();
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
                {
                    sim.IsLookingForWork = js.IsLookingForWork;
                    sim.DesiredSalaryCents = js.DesiredSalary.Cents;
                }
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

            var tileQuery = new QueryDescription().WithAll<GenName, TilePosition>();
            world.Query(in tileQuery, (Entity entity, ref TilePosition position) =>
            {
                if (simByEntity.TryGetValue(entity, out var sim))
                {
                    sim.HasTilePosition = true;
                    sim.TileX = position.Tile.X;
                    sim.TileY = position.Tile.Y;
                }
            });

            var moveTargetQuery = new QueryDescription().WithAll<GenName, MoveTarget>();
            world.Query(in moveTargetQuery, (Entity entity, ref MoveTarget target) =>
            {
                if (simByEntity.TryGetValue(entity, out var sim))
                {
                    sim.HasMoveTarget = target.HasTarget;
                    sim.MoveTargetX = target.Tile.X;
                    sim.MoveTargetY = target.Tile.Y;
                }
            });

            var movementQuery = new QueryDescription().WithAll<GenName, MovementState>();
            world.Query(in movementQuery, (Entity entity, ref MovementState movement) =>
            {
                if (simByEntity.TryGetValue(entity, out var sim))
                {
                    sim.MoveFromX = movement.From.X;
                    sim.MoveFromY = movement.From.Y;
                    sim.MoveToX = movement.To.X;
                    sim.MoveToY = movement.To.Y;
                    sim.MoveProgress = movement.Progress;
                    sim.SubTileX = movement.IsMoving
                        ? movement.From.X + (movement.To.X - movement.From.X) * movement.Progress
                        : 0f;
                    sim.SubTileY = movement.IsMoving
                        ? movement.From.Y + (movement.To.Y - movement.From.Y) * movement.Progress
                        : 0f;
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
                    sim.WalletBalanceCents = wallet.Balance.Cents;
            });

            var savingsQuery = new QueryDescription().WithAll<GenName, SavingsAccount>();
            world.Query(in savingsQuery, (Entity entity, ref SavingsAccount savings) =>
            {
                if (simByEntity.TryGetValue(entity, out var sim))
                    sim.SavingsBalanceCents = savings.Balance.Cents;
            });

            var consumptionQuery = new QueryDescription().WithAll<GenName, ConsumptionState>();
            world.Query(in consumptionQuery, (Entity entity, ref ConsumptionState consumption) =>
            {
                if (simByEntity.TryGetValue(entity, out var sim))
                {
                    sim.UnmetDemandCents = consumption.UnmetDemand.Cents;
                    sim.LastSpendCents = consumption.LastCycleSpent.Cents;
                    sim.ConsumptionLifetimeCents = consumption.LifetimeSpent.Cents;
                }
            });

            var pensionQuery = new QueryDescription().WithAll<GenName, PensionAccount>();
            world.Query(in pensionQuery, (Entity entity, ref PensionAccount pension) =>
            {
                if (simByEntity.TryGetValue(entity, out var sim))
                {
                    sim.PensionEmployeeCents = pension.EmployeeContributions.Cents;
                    sim.PensionEmployerCents = pension.EmployerContributions.Cents;
                    sim.PensionCycles = pension.ContributingCycles;
                }
            });

            var employeeQuery = new QueryDescription().WithAll<Employee>();
            world.Query(in employeeQuery, (Entity entity, ref Employee employee) =>
            {
                if (!simByEntity.TryGetValue(entity, out var sim))
                    return;
                sim.EmployerNetworkId = GetNetworkId(world, employee.Company);
                sim.OfficeNetworkId = GetNetworkId(world, employee.Office);
                sim.Position = employee.Position;
                sim.SalaryCents = employee.SalaryPerCycle.Cents;
                sim.Seniority = employee.Seniority.ToString();
                sim.HiredAtTick = employee.HiredAtTick;
            });

            var claimQuery = new QueryDescription().WithAll<WageClaim>();
            world.Query(in claimQuery, (Entity entity, ref WageClaim claim) =>
            {
                if (!simByEntity.TryGetValue(entity, out var sim))
                    return;
                sim.ClaimCompanyNetworkId = GetNetworkId(world, claim.Company);
                sim.UnpaidGrossCents = claim.UnpaidGross.Cents;
                sim.UnpaidNetCents = claim.UnpaidNet.Cents;
                sim.SeveranceCents = claim.Severance.Cents;
                sim.ClaimCycles = claim.CyclesInArrears;
                sim.ClaimFirstTick = claim.FirstMissedTick;
            });

            var lifecycleQuery = new QueryDescription().WithAll<GenName, Lifecycle>();
            world.Query(in lifecycleQuery, (Entity entity, ref Lifecycle lifecycle) =>
            {
                if (!simByEntity.TryGetValue(entity, out var sim))
                    return;
                sim.Stage = lifecycle.Stage.ToString();
                sim.AgeTicks = lifecycle.AgeTicks;
                sim.StageEnteredTick = lifecycle.StageEnteredTick;
                sim.StageCause = lifecycle.LastCause.ToString();
                sim.LastAgedTick = lifecycle.LastAgedTick;
                sim.MaxAgeFlagged = lifecycle.MaxAgeFlagged;
            });

            world.Query(in new QueryDescription().WithAll<GenName, SkillProfile>(),
                (Entity entity, ref SkillProfile profile) =>
                {
                    if (!simByEntity.TryGetValue(entity, out var sim))
                        return;
                    sim.SkillProgramming = profile.Programming;
                    sim.SkillArt = profile.Art;
                    sim.SkillCooking = profile.Cooking;
                    sim.SkillMedical = profile.Medical;
                    sim.SkillEngineering = profile.Engineering;
                    sim.SkillSales = profile.Sales;
                });

            world.Query(in new QueryDescription().WithAll<GenName, Health>(),
                (Entity entity, ref Health health) =>
                {
                    if (!simByEntity.TryGetValue(entity, out var sim))
                        return;
                    sim.HealthValue = health.Value;
                    sim.HealthDeclineRate = health.DeclineRate;
                    sim.HealthLastCareTick = health.LastCareTick;
                    sim.ChronicConditions = health.ChronicConditions;
                });

            world.Query(in new QueryDescription().WithAll<GenName, Enrollment>(),
                (Entity entity, ref Enrollment enrollment) =>
                {
                    if (!simByEntity.TryGetValue(entity, out var sim))
                        return;
                    sim.EnrollmentSchoolNetworkId = GetNetworkId(world, enrollment.School);
                    sim.EnrollmentAttendance = enrollment.Attendance;
                    sim.EnrollmentMonths = enrollment.MonthsAttended;
                    sim.IsUnenrolled = enrollment.IsUnenrolled;
                });

            world.Query(in new QueryDescription().WithAll<GenName, EmploymentRecord>(),
                (Entity entity, ref EmploymentRecord record) =>
                {
                    if (!simByEntity.TryGetValue(entity, out var sid))
                        return;
                    sid.EmploymentAttendance = record.Attendance;
                    sid.EmploymentPerformance = record.Performance;
                    sid.EmploymentTenureCycles = record.TenureCycles;
                    sid.EmploymentWarnings = record.Warnings;
                    sid.EmploymentShiftsCompleted = record.ShiftsCompleted;
                    sid.EmploymentShiftsExpected = record.ShiftsExpected;
                    sid.EmploymentInterruptions = record.Interruptions;
                    sid.EmploymentConsecutiveLowMonths = record.ConsecutiveLowMonths;
                    sid.EmploymentHighPerformanceMonths = record.HighPerformanceMonths;
                    sid.EmploymentLastReviewTick = record.LastReviewTick;
                });

            world.Query(in new QueryDescription().WithAll<GenName, WorkSchedule>(),
                (Entity entity, ref WorkSchedule schedule) =>
                {
                    if (!simByEntity.TryGetValue(entity, out var sid))
                        return;
                    sid.WorkOfficeNetworkId = GetNetworkId(world, schedule.Office);
                    sid.IsAtWork = schedule.IsAtWork;
                    sid.ShiftTicksRemaining = schedule.ShiftTicksRemaining;
                    sid.LastShiftStartTick = schedule.LastShiftStartTick;
                    sid.LastShiftEndTick = schedule.LastShiftEndTick;
                    sid.WorkShiftsCompleted = schedule.ShiftsCompleted;
                    sid.WorkShiftsInterrupted = schedule.ShiftsInterrupted;
                });

            world.Query(in new QueryDescription().WithAll<GenName, RetirementIncome>(),
                (Entity entity, ref RetirementIncome income) =>
                {
                    if (!simByEntity.TryGetValue(entity, out var sid))
                        return;
                    sid.RetirementMonthlyCents = income.MonthlyPension.Cents;
                    sid.RetirementStartedTick = income.StartedTick;
                    sid.RetirementReduced = income.Reduced;
                    sid.RetirementMeansTested = income.MeansTested;
                    sid.RetirementFund = income.FundReference;
                });

            world.Query(in new QueryDescription().WithAll<GenName, UnemploymentRecord>(),
                (Entity entity, ref UnemploymentRecord record) =>
                {
                    if (!simByEntity.TryGetValue(entity, out var sid))
                        return;
                    sid.UnemploymentStartedTick = record.StartedTick;
                    sid.UnemploymentBenefitCents = record.BenefitPerCycle.Cents;
                    sid.UnemploymentCyclesClaimed = record.CyclesClaimed;
                    sid.UnemploymentMaxCycles = record.MaxCycles;
                    sid.UnemploymentEligible = record.Eligible;
                    sid.UnemploymentUnpaidCycles = record.UnpaidCycles;
                });

            world.Query(in new QueryDescription().WithAll<GenName, Dead>(),
                (Entity entity, ref Dead dead) =>
                {
                    if (!simByEntity.TryGetValue(entity, out var sid))
                        return;
                    sid.IsDead = true;
                    sid.DeathTick = dead.Tick;
                    sid.DeathCause = dead.Cause.ToString();
                });

            var schools = new List<PersistedSchool>();
            world.Query(in new QueryDescription().WithAll<School, NetworkId>(),
                (Entity entity, ref School school, ref NetworkId nid) =>
                {
                    schools.Add(new PersistedSchool
                    {
                        NetworkId = nid.Value,
                        Name = school.Name,
                        District = school.District,
                        Capacity = school.Capacity,
                        Quality = school.Quality,
                        Emphasis = (int)school.Emphasis,
                        MonthlyCostCents = school.MonthlyCost.Cents,
                        OwnerNetworkId = GetNetworkId(world, school.Owner),
                        Enrolled = school.Enrolled
                    });
                });

            var companyQuery = new QueryDescription().WithAll<CompanyName, NetworkId>();
            world.Query(in companyQuery, (Entity entity, ref CompanyName cname, ref NetworkId nid) =>
            {
                var company = new PersistedCompany
                {
                    Name = cname.Name,
                    NetworkId = nid.Value
                };

                if (world.Has<CompanyAccount>(entity))
                {
                    ref var account = ref world.Get<CompanyAccount>(entity);
                    company.HasAccount = true;
                    company.BalanceCents = account.Balance.Cents;
                    company.ReservedCents = account.ReservedForPayroll.Cents;
                    company.RevenueLastCycleCents = account.RevenueLastCycle.Cents;
                    company.WagesLastCycleCents = account.WagesLastCycle.Cents;
                    company.CostsLastCycleCents = account.CostsLastCycle.Cents;
                    company.InterestPaidLastCycleCents = account.InterestPaidLastCycle.Cents;
                    company.ArrearsCents = account.Arrears.Cents;
                    company.TaxArrearsCents = account.TaxArrears.Cents;
                    company.LossCarryForwardCents = account.LossCarryForward.Cents;
                    company.LossCarryForwardExpiresAtTick = account.LossCarryForwardExpiresAtTick;
                    company.Solvency = account.State.ToString();
                    company.StateChangedTick = account.StateChangedAtTick;
                    company.ArrearsCycles = account.ArrearsCycles;
                    company.InsolventCycles = account.InsolventCycles;
                    company.HiringFrozen = account.HiringFrozen;
                }

                companies.Add(company);
            });

            var officeQuery = new QueryDescription().WithAll<Office, NetworkId>();
            world.Query(in officeQuery, (Entity entity, ref Office office, ref NetworkId nid) =>
            {
                offices.Add(new PersistedOffice
                {
                    NetworkId = nid.Value,
                    CompanyNetworkId = GetNetworkId(world, office.Company),
                    Name = office.OfficeName,
                    Capacity = office.Capacity,
                    OperatingCostCents = office.OperatingCostPerCycle.Cents,
                    SeatsUsed = office.SeatsUsed,
                    IsClosed = office.IsClosed
                });
            });

            var postingQuery = new QueryDescription().WithAll<JobPosting, NetworkId>();
            world.Query(in postingQuery, (Entity entity, ref JobPosting posting, ref NetworkId nid) =>
            {
                postings.Add(new PersistedPosting
                {
                    NetworkId = nid.Value,
                    CompanyNetworkId = GetNetworkId(world, posting.Company),
                    OfficeNetworkId = GetNetworkId(world, posting.Office),
                    Position = posting.Position,
                    SalaryCents = posting.SalaryPerCycle.Cents,
                    Requirements = (int)posting.Requirements,
                    OpenSeats = OfficeSeatAccounting.OpenSeatsForPosting(world, entity),
                    TotalSeats = posting.TotalSeats,
                    PostedAtTick = posting.PostedAtTick,
                    IsClosed = posting.IsClosed
                });
            });

            var appQuery = new QueryDescription().WithAll<JobApplication>();
            world.Query(in appQuery, (ref JobApplication app) =>
            {
                applications.Add(new PersistedApplication
                {
                    ApplicantNetworkId = GetNetworkId(world, app.Applicant),
                    PostingNetworkId = GetNetworkId(world, app.JobPosting),
                    CompanyNetworkId = GetNetworkId(world, app.Company),
                    Position = app.Position,
                    Status = app.Status.ToString(),
                    MatchScore = app.MatchScore,
                    ApplicationDate = app.ApplicationDate
                });
            });

            foreach (var loan in economy.Loans.Loans)
            {
                loans.Add(new PersistedLoan
                {
                    LoanId = loan.LoanId,
                    CompanyNetworkId = loan.CompanyNetworkId,
                    PrincipalCents = loan.Principal.Cents,
                    OutstandingCents = loan.Outstanding.Cents,
                    AnnualRate = loan.AnnualRate,
                    TermCycles = loan.TermCycles,
                    CyclesRemaining = loan.CyclesRemaining,
                    PaymentPerCycleCents = loan.PaymentPerCycle.Cents,
                    State = loan.State.ToString(),
                    MissedPayments = loan.MissedPayments,
                    IssuedAtTick = loan.IssuedAtTick
                });
            }

            var metricHistory = economy.MetricHistory.Select(snapshot => new PersistedEconomySnapshot
            {
                Tick = snapshot.Tick,
                Year = snapshot.Year,
                HouseholdCashCents = snapshot.HouseholdCash.Cents,
                HouseholdSavingsCents = snapshot.HouseholdSavings.Cents,
                CompanyCashCents = snapshot.CompanyCash.Cents,
                CompanyDebtCents = snapshot.CompanyDebt.Cents,
                PensionFundCents = snapshot.PensionFund.Cents,
                GovernmentFundCents = snapshot.GovernmentFund.Cents,
                BankFundsCents = snapshot.BankFunds.Cents,
                TotalMoneySupplyCents = snapshot.TotalMoneySupply.Cents,
                WagesPaidCents = snapshot.WagesPaidThisYear.Cents,
                RevenueCents = snapshot.RevenueThisYear.Cents,
                TaxCents = snapshot.TaxThisYear.Cents,
                BasicIncomeCents = snapshot.BasicIncomeThisYear.Cents,
                ArrearsCents = snapshot.Arrears.Cents,
                UnemploymentRate = snapshot.UnemploymentRate,
                VacancyRate = snapshot.VacancyRate,
                WageShare = snapshot.WageShare,
                ProfitShare = snapshot.ProfitShare,
                Gini = snapshot.Gini,
                HomelessHouseholds = snapshot.HomelessHouseholds,
                BankruptCompanies = snapshot.BankruptCompanies
            }).ToList();

            foreach (var (company, revenue) in economy.PendingRevenue)
            {
                var companyNetworkId = GetNetworkId(world, company);
                foreach (var spend in revenue.Spends)
                {
                    pendingRevenue.Add(new PersistedPendingRevenue
                    {
                        CompanyNetworkId = companyNetworkId,
                        SimNetworkId = spend.SimNetworkId,
                        Category = spend.Category,
                        AmountCents = spend.Amount.Cents,
                        Tick = spend.Tick
                    });
                }
            }

            var properties = new List<PersistedProperty>();
            world.Query(in new QueryDescription().WithAll<Property, NetworkId>(),
                (Entity entity, ref Property property, ref NetworkId nid) =>
                {
                    properties.Add(new PersistedProperty
                    {
                        NetworkId = nid.Value,
                        OwnerNetworkId = GetNetworkId(world, property.Owner),
                        Name = property.Name,
                        District = property.District,
                        UnitCount = property.UnitCount,
                        Condition = property.Condition,
                        Neighbourhood = property.NeighbourhoodScore,
                        MonthlyUpkeepCents = property.MonthlyUpkeep.Cents,
                        IsShelter = property.IsShelter
                    });
                });

            var housingUnits = new List<PersistedHousingUnit>();
            world.Query(in new QueryDescription().WithAll<HousingUnit, NetworkId>(),
                (Entity entity, ref HousingUnit unit, ref NetworkId nid) =>
                {
                    housingUnits.Add(new PersistedHousingUnit
                    {
                        NetworkId = nid.Value,
                        PropertyNetworkId = GetNetworkId(world, unit.Property),
                        UnitNumber = unit.UnitNumber,
                        Tier = unit.Tier.ToString(),
                        Capacity = unit.Capacity,
                        BaseRentCents = unit.BaseRent.Cents,
                        Condition = unit.Condition,
                        IsVacant = unit.IsVacant,
                        OwnerNetworkId = GetNetworkId(world, unit.Owner),
                        PurchasePriceCents = unit.PurchasePrice.Cents,
                        OccupantCount = unit.OccupantCount,
                        IsCondemned = unit.IsCondemned
                    });
                });

            var households = new List<PersistedHousehold>();
            world.Query(in new QueryDescription().WithAll<Household, NetworkId>(),
                (Entity entity, ref Household household, ref NetworkId nid) =>
                {
                    households.Add(new PersistedHousehold
                    {
                        NetworkId = nid.Value,
                        HeadNetworkId = GetNetworkId(world, household.Head),
                        UnitNetworkId = GetNetworkId(world, household.Unit),
                        Tenure = household.Tenure.ToString(),
                        MonthlyRentCents = household.MonthlyRent.Cents,
                        MoveInTick = household.MoveInTick,
                        ArrearsCycles = household.ArrearsCycles,
                        RentArrearsCents = household.RentArrears.Cents,
                        UtilityArrearsCents = household.UtilityArrears.Cents,
                        DebtCents = household.Debt.Cents,
                        LastRentTick = household.LastRentTick,
                        LastUtilityTick = household.LastUtilityTick,
                        NoticeStage = household.NoticeStage,
                        NoticeStartedTick = household.NoticeStartedTick,
                        CureDeadlineTick = household.CureDeadlineTick,
                        LastMoveTick = household.LastMoveTick,
                        OverburdenMonths = household.OverburdenMonths,
                        NoUtilities = household.NoUtilities,
                        MemberCount = household.MemberCount
                    });
                });

            var householdMembers = new List<PersistedHouseholdMember>();
            world.Query(in new QueryDescription().WithAll<HouseholdMember>(),
                (Entity entity, ref HouseholdMember member) =>
                {
                    householdMembers.Add(new PersistedHouseholdMember
                    {
                        HouseholdNetworkId = GetNetworkId(world, member.Household),
                        SimNetworkId = GetNetworkId(world, entity),
                        Role = member.Role.ToString()
                    });
                });

            var constructionProjects = new List<PersistedConstructionProject>();
            world.Query(in new QueryDescription().WithAll<ConstructionProject, NetworkId>(),
                (Entity entity, ref ConstructionProject project, ref NetworkId nid) =>
                {
                    constructionProjects.Add(new PersistedConstructionProject
                    {
                        ProjectId = project.ProjectId == 0 ? nid.Value : project.ProjectId,
                        DeveloperNetworkId = GetNetworkId(world, project.Developer),
                        District = project.District,
                        Tier = project.Tier.ToString(),
                        Units = project.Units,
                        CostCents = project.Cost.Cents,
                        PaidToDateCents = project.PaidToDate.Cents,
                        MonthsRemaining = project.MonthsRemaining,
                        TotalMonths = project.TotalMonths,
                        StartedAtTick = project.StartedAtTick
                    });
                });

            foreach (var (simEntity, sim) in simByEntity)
            {
                if (!economy.Housing.HouseholdBySim.TryGetValue(simEntity, out var householdEntity))
                    continue;
                if (!world.IsAlive(householdEntity) || !world.Has<Household>(householdEntity))
                    continue;

                var household = world.Get<Household>(householdEntity);
                sim.HouseholdNetworkId = GetNetworkId(world, householdEntity);
                sim.HouseholdUnitNetworkId = GetNetworkId(world, household.Unit);
                sim.Tenure = household.Tenure.ToString();
                if (world.IsAlive(household.Unit) && world.Has<HousingUnit>(household.Unit))
                    sim.HousingTier = world.Get<HousingUnit>(household.Unit).Tier.ToString();
            }

            var housingMetricHistory = economy.Housing.MetricHistory.Select(snapshot => new PersistedHousingSnapshot
            {
                Tick = snapshot.Tick,
                Year = snapshot.Year,
                Properties = snapshot.Properties,
                Units = snapshot.Units,
                OccupiedUnits = snapshot.OccupiedUnits,
                VacantUnits = snapshot.VacantUnits,
                ShelterBeds = snapshot.ShelterBeds,
                ShelteredHouseholds = snapshot.ShelteredHouseholds,
                HomelessHouseholds = snapshot.HomelessHouseholds,
                OvercrowdedUnits = snapshot.OvercrowdedUnits,
                UnitsBelowCondition = snapshot.UnitsBelowCondition,
                VacancyRate = snapshot.VacancyRate,
                RentIndex = snapshot.RentIndex,
                RentRollCents = snapshot.RentRoll.Cents,
                RentCollectedCents = snapshot.RentCollected.Cents,
                RentArrearsCents = snapshot.RentArrears.Cents,
                UtilityArrearsCents = snapshot.UtilityArrears.Cents,
                UpkeepPaidCents = snapshot.UpkeepPaid.Cents,
                EvictionsThisYear = snapshot.EvictionsThisYear,
                ConstructionUnits = snapshot.ConstructionUnits,
                MedianRentBurden = snapshot.MedianRentBurden,
                ShelterOccupancy = snapshot.ShelterOccupancy
            }).ToList();

            var market = economy.Housing.Market;
            var housingMarket = new PersistedHousingMarket
            {
                Index = market.Index,
                LastUpdatedTick = market.LastUpdatedTick,
                EvictionsThisYear = market.EvictionsThisYear,
                MovesThisYear = market.MovesThisYear,
                CompletionsThisYear = market.CompletionsThisYear,
                RentCollectedThisMonthCents = market.RentCollectedThisMonth.Cents,
                UtilityCollectedThisMonthCents = market.UtilityCollectedThisMonth.Cents,
                UpkeepPaidThisMonthCents = market.UpkeepPaidThisMonth.Cents,
                RentCollectedThisYearCents = market.RentCollectedThisYear.Cents,
                UtilityCollectedThisYearCents = market.UtilityCollectedThisYear.Cents,
                UpkeepPaidThisYearCents = market.UpkeepPaidThisYear.Cents
            };
            var districts = market.DistrictCapacity.Keys
                .Union(market.DistrictUnits.Keys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);
            foreach (var district in districts)
            {
                housingMarket.Districts.Add(new PersistedDistrict
                {
                    Name = district,
                    Capacity = market.CapacityFor(district),
                    Units = market.UnitsIn(district)
                });
            }

            var lifecycleMetrics = economy.Life.MetricHistory.Select(snapshot => new PersistedPopulationSnapshot
            {
                Tick = snapshot.Tick,
                Year = snapshot.Year,
                Total = snapshot.Total,
                Education = snapshot.Education,
                Work = snapshot.Work,
                Retirement = snapshot.Retirement,
                Elderly = snapshot.Elderly,
                StudentsEnrolled = snapshot.StudentsEnrolled,
                GraduatesThisYear = snapshot.GraduatesThisYear,
                Employed = snapshot.Employed,
                Unemployed = snapshot.Unemployed,
                RetiredThisYear = snapshot.RetiredThisYear,
                DeathsThisYear = snapshot.DeathsThisYear,
                ArrivalsThisYear = snapshot.ArrivalsThisYear,
                AverageAge = snapshot.AverageAge,
                MedianAge = snapshot.MedianAge,
                AverageHealth = snapshot.AverageHealth,
                AverageAgeAtDeath = snapshot.AverageAgeAtDeath,
                LifeExpectancy = snapshot.LifeExpectancy,
                PensionReplacementRate = snapshot.PensionReplacementRate,
                PensionFundingRatio = snapshot.PensionFundingRatio,
                WorkingAgeShare = snapshot.WorkingAgeShare,
                OldAgeDependencyRatio = snapshot.OldAgeDependencyRatio,
                PyramidBuckets = snapshot.PyramidBuckets.ToList(),
                DeathCauses = snapshot.DeathCauses.Select(cause => new PersistedDeathCauseCount
                {
                    Cause = cause.Cause,
                    Count = cause.Count
                }).ToList()
            }).ToList();

            var mortalityLog = economy.Life.MortalityLog.Select(record => new PersistedMortalityRecord
            {
                Tick = record.Tick,
                SimNetworkId = record.SimNetworkId,
                Name = record.Name,
                Age = record.Age,
                Cause = record.Cause.ToString(),
                Wealth = record.Wealth
            }).ToList();

            var estateQueue = economy.Life.EstateQueue.Select(request => new PersistedEstateRequest
            {
                SimNetworkId = request.SimNetworkId,
                HouseholdNetworkId = GetNetworkId(world, request.Household),
                UnitNetworkId = GetNetworkId(world, request.Unit),
                OwnedUnitNetworkId = GetNetworkId(world, request.OwnedUnit),
                Tick = request.Tick,
                Name = request.Name,
                Age = request.Age,
                Cause = request.Cause.ToString(),
                Heirs = request.Heirs.Select(heir => GetNetworkId(world, heir)).Where(id => id != 0).ToList(),
                HouseholdRentArrears = request.HouseholdRentArrears,
                HouseholdUtilityArrears = request.HouseholdUtilityArrears
            }).ToList();

            var immigration = new PersistedImmigration
            {
                TargetPopulation = economy.Life.Immigration.TargetPopulation,
                Enabled = economy.Life.Immigration.Enabled,
                ArrivalsThisYear = economy.Life.ImmigrationState.ArrivalsThisYear,
                ArrivalBudget = economy.Life.ImmigrationState.ArrivalBudget,
                Year = economy.Life.ImmigrationState.Year,
                CancelledThisYear = economy.Life.ImmigrationState.CancelledThisYear,
                Paused = economy.Life.ImmigrationState.Paused,
                Pending = economy.Life.ImmigrationState.Pending.Select(pending => new PersistedPendingArrival
                {
                    Name = pending.Name,
                    Age = pending.Age,
                    QueuedTick = pending.QueuedTick,
                    ExpiresTick = pending.ExpiresTick,
                    SkillProgramming = pending.Profile.Programming,
                    SkillArt = pending.Profile.Art,
                    SkillCooking = pending.Profile.Cooking,
                    SkillMedical = pending.Profile.Medical,
                    SkillEngineering = pending.Profile.Engineering,
                    SkillSales = pending.Profile.Sales
                }).ToList()
            };

            var mapState = new PersistedMap();
            var persistedPlacements = new List<PersistedPlacement>();
            if (mapContext is not null)
            {
                var options = mapContext.Options;
                var generated = mapContext.Generated;
                mapState = new PersistedMap
                {
                    MapId = options.MapId,
                    Version = mapContext.Version,
                    Seed = options.Seed,
                    Width = options.Width,
                    Height = options.Height,
                    TileSize = options.TileSize,
                    Tiles = generated.Tiles.Select(flags => (byte)flags).ToArray(),
                    CanonicalSignature = generated.CanonicalSignature,
                    Lots = generated.Lots.Select(lot => new PersistedLot
                    {
                        Id = lot.Id.Value,
                        MinX = lot.Bounds.MinX,
                        MinY = lot.Bounds.MinY,
                        MaxX = lot.Bounds.MaxX,
                        MaxY = lot.Bounds.MaxY,
                        Zone = lot.Zone.ToString()
                    }).ToList(),
                    SpawnPoints = generated.SpawnPoints.Select(tile => new PersistedCoord
                    {
                        X = tile.X,
                        Y = tile.Y
                    }).ToList()
                };

                foreach (var pair in mapContext.Map.Placements.OrderBy(pair => pair.Key.Value))
                {
                    var placement = pair.Value;
                    persistedPlacements.Add(new PersistedPlacement
                    {
                        NetworkId = pair.Key.Value,
                        Kind = placement.Kind.ToString(),
                        X = placement.Anchor.X,
                        Y = placement.Anchor.Y,
                        Width = placement.Footprint.Width,
                        Height = placement.Footprint.Height,
                        EntryX = placement.Entry.X,
                        EntryY = placement.Entry.Y,
                        Rotation = placement.Rotation
                    });
                }
            }

            return new PersistedWorldState
            {
                SchemaVersion = PersistedWorldState.CurrentSchemaVersion,
                Tick = currentTick,
                RandomSeed = economy.Random.Seed,
                Map = mapState,
                Placements = persistedPlacements,
                HousingMigrated = true,
                LifecycleMigrated = true,
                Schools = schools,
                LifecycleMetricHistory = lifecycleMetrics,
                MortalityLog = mortalityLog,
                EstateQueue = estateQueue,
                Immigration = immigration,
                Sims = sims,
                Companies = companies,
                Offices = offices,
                Postings = postings,
                Applications = applications,
                Loans = loans,
                PendingRevenue = pendingRevenue,
                MetricHistory = metricHistory,
                Properties = properties,
                HousingUnits = housingUnits,
                Households = households,
                HouseholdMembers = householdMembers,
                ConstructionProjects = constructionProjects,
                HousingMetricHistory = housingMetricHistory,
                HousingMarket = housingMarket,
                Fiscal = new PersistedFiscal
                {
                    GovernmentBalanceCents = economy.Government.Balance.Cents,
                    IncomeTaxCollectedCents = economy.Government.IncomeTaxCollected.Cents,
                    CorporateTaxCollectedCents = economy.Government.CorporateTaxCollected.Cents,
                    BenefitsPaidCents = economy.Government.BenefitsPaid.Cents,
                    OfficeCostsCollectedCents = economy.Government.OfficeCostsCollected.Cents,
                    PensionBalanceCents = economy.Pension.Balance.Cents,
                    PensionContributionsCents = economy.Pension.ContributionsThisCycle.Cents,
                    BankBalanceCents = economy.Bank.Balance.Cents,
                    BankInterestPoolCents = economy.Bank.InterestPool.Cents,
                    WagesPaidThisYearCents = economy.WagesPaidThisYear.Cents,
                    RevenueThisYearCents = economy.RevenueThisYear.Cents,
                    TaxThisYearCents = economy.TaxThisYear.Cents,
                    BasicIncomeThisYearCents = economy.BasicIncomeThisYear.Cents,
                    ConsumptionThisYearCents = economy.ConsumptionThisYear.Cents
                }
            };
        }
    }

    private TileCoord ResolveRestoredTile(PersistedSim sim)
    {
        if (mapContext is null)
            return new TileCoord(sim.TileX, sim.TileY);

        var tile = sim.HasTilePosition
            ? new TileCoord(sim.TileX, sim.TileY)
            : mapContext.Options.ToTile(sim.X, sim.Y);

        if (!mapContext.Options.Bounds.Contains(tile) || !mapContext.Map.IsWalkable(tile))
            tile = MapSpawner.FindSpawn(mapContext, sim.NetworkId);

        return tile;
    }

    private void RestorePlacements(
        PersistedWorldState state,
        Dictionary<ulong, Entity> properties,
        Dictionary<ulong, Entity> offices,
        Dictionary<ulong, Entity> schools)
    {
        if (mapContext is null || world is null)
            return;

        var entities = new Dictionary<ulong, Entity>();
        foreach (var pair in properties)
            entities[pair.Key] = pair.Value;
        foreach (var pair in offices)
            entities[pair.Key] = pair.Value;
        foreach (var pair in schools)
            entities[pair.Key] = pair.Value;

        foreach (var persisted in state.Placements)
        {
            if (!entities.TryGetValue(persisted.NetworkId, out var entity) || !world.IsAlive(entity))
                continue;
            if (world.Has<Placement>(entity))
                continue;

            var kind = Enum.TryParse<BuildingKind>(persisted.Kind, out var parsedKind)
                ? parsedKind
                : BuildingKind.Residential;
            var placement = new Placement(
                new TileCoord(persisted.X, persisted.Y),
                new Footprint(persisted.Width, persisted.Height),
                new TileCoord(persisted.EntryX, persisted.EntryY),
                kind,
                persisted.Rotation);

            var result = mapContext.Placement.RestorePlacement(
                entity,
                new NetworkId(persisted.NetworkId),
                placement);
            if (!result.Success)
                throw new InvalidOperationException(
                    $"Map restore rejected building {persisted.NetworkId}: {result.Error} {result.Message}");
        }

        foreach (var pair in entities.OrderBy(pair => pair.Key))
        {
            var entity = pair.Value;
            if (!world.IsAlive(entity) || world.Has<Placement>(entity))
                continue;

            var networkId = new NetworkId(pair.Key);
            BuildingKind kind;
            Footprint footprint;
            string district;

            if (world.Has<Property>(entity))
            {
                var property = world.Get<Property>(entity);
                kind = property.IsShelter ? BuildingKind.Civic : BuildingKind.Residential;
                footprint = BuildingFootprints.ForProperty(property.UnitCount, property.IsShelter);
                district = property.District;
            }
            else if (world.Has<Office>(entity))
            {
                var office = world.Get<Office>(entity);
                kind = BuildingKind.Office;
                footprint = BuildingFootprints.ForOffice(office.Capacity);
                district = "Central";
            }
            else if (world.Has<School>(entity))
            {
                var school = world.Get<School>(entity);
                kind = BuildingKind.School;
                footprint = BuildingFootprints.ForSchool(school.Capacity);
                district = school.District;
            }
            else
            {
                continue;
            }

            var result = mapContext.Placement.TryPlace(entity, networkId, kind, footprint, district);
            if (!result.Success)
                throw new InvalidOperationException(
                    $"Map migration could not place building {pair.Key}: {result.Error} {result.Message}");
        }

        var issues = mapContext.Map.Reconcile(world);
        if (issues.Count > 0)
            throw new InvalidOperationException(
                "Map restore verification failed: " +
                string.Join("; ", issues.Take(10).Select(issue => $"{issue.Code}: {issue.Message}")));
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

            RandomSeed = state.RandomSeed == 0 ? 1 : state.RandomSeed;
            Initialize();

            if (state.Map.Tiles.Length > 0)
            {
                var options = new Genelife.Map.MapOptions
                {
                    MapId = state.Map.MapId,
                    Version = state.Map.Version,
                    Seed = state.Map.Seed,
                    Width = state.Map.Width,
                    Height = state.Map.Height,
                    TileSize = state.Map.TileSize
                };
                var generated = new GeneratedMap
                {
                    Options = options,
                    Tiles = state.Map.Tiles.Select(flags => (TileFlags)flags).ToArray(),
                    Lots = state.Map.Lots.Select(lot => new MapLot
                    {
                        Id = new LotId(lot.Id),
                        Bounds = new MapBounds(lot.MinX, lot.MinY, lot.MaxX, lot.MaxY),
                        Zone = Enum.TryParse<MapZone>(lot.Zone, out var zone) ? zone : MapZone.Mixed
                    }).ToList(),
                    SpawnPoints = state.Map.SpawnPoints.Select(point => new TileCoord(point.X, point.Y)).ToList(),
                    CanonicalSignature = state.Map.CanonicalSignature
                };
                mapContext!.LoadRestored(generated, state.Map.Version);
            }

            var simEntities = new Dictionary<ulong, Entity>();
            var companyEntities = new Dictionary<ulong, Entity>();
            var officeEntities = new Dictionary<ulong, Entity>();
            var postingEntities = new Dictionary<ulong, Entity>();

            foreach (var company in state.Companies)
            {
                var entity = world!.Create(new CompanyName(company.Name));
                var networkId = new NetworkId(company.NetworkId);
                world.Add(entity, networkId);
                networkIds.AllocateWithValue(entity, networkId);
                companyEntities[company.NetworkId] = entity;

                var account = CompanyAccount.Create(new Money(company.BalanceCents), company.StateChangedTick);
                account.ReservedForPayroll = new Money(company.ReservedCents);
                account.RevenueLastCycle = new Money(company.RevenueLastCycleCents);
                account.WagesLastCycle = new Money(company.WagesLastCycleCents);
                account.CostsLastCycle = new Money(company.CostsLastCycleCents);
                account.InterestPaidLastCycle = new Money(company.InterestPaidLastCycleCents);
                account.Arrears = new Money(company.ArrearsCents);
                account.TaxArrears = new Money(company.TaxArrearsCents);
                account.LossCarryForward = new Money(company.LossCarryForwardCents);
                account.LossCarryForwardExpiresAtTick = company.LossCarryForwardExpiresAtTick;
                account.ArrearsCycles = company.ArrearsCycles;
                account.InsolventCycles = company.InsolventCycles;
                account.HiringFrozen = company.HiringFrozen;
                if (Enum.TryParse<CompanySolvency>(company.Solvency, out var solvency))
                    account.State = solvency;
                world.Add(entity, account);
            }

            foreach (var office in state.Offices)
            {
                if (!companyEntities.TryGetValue(office.CompanyNetworkId, out var company))
                    continue;
                var entity = world!.Create(new Office(
                    office.Name,
                    company,
                    office.Capacity,
                    new Money(office.OperatingCostCents),
                    office.SeatsUsed,
                    office.IsClosed));
                var networkId = new NetworkId(office.NetworkId);
                world.Add(entity, networkId);
                networkIds.AllocateWithValue(entity, networkId);
                officeEntities[office.NetworkId] = entity;
            }

            foreach (var posting in state.Postings)
            {
                if (!companyEntities.TryGetValue(posting.CompanyNetworkId, out var company))
                    continue;
                if (!officeEntities.TryGetValue(posting.OfficeNetworkId, out var office))
                    continue;

                var entity = world!.Create(new JobPosting(
                    company,
                    office,
                    posting.Position,
                    new Money(posting.SalaryCents),
                    (Skill)posting.Requirements,
                    posting.OpenSeats,
                    posting.TotalSeats,
                    posting.PostedAtTick,
                    posting.IsClosed));
                var networkId = new NetworkId(posting.NetworkId);
                world.Add(entity, networkId);
                networkIds.AllocateWithValue(entity, networkId);
                postingEntities[posting.NetworkId] = entity;

                for (var i = 0; i < posting.OpenSeats; i++)
                    world.Create(new OpenPosition(entity));
            }

            var schoolEntities = new Dictionary<ulong, Entity>();
            foreach (var school in state.Schools)
            {
                Entity owner = Entity.Null;
                if (school.OwnerNetworkId != 0)
                    companyEntities.TryGetValue(school.OwnerNetworkId, out owner);

                var component = new Genelife.Components.Life.School(
                    school.Name,
                    school.District,
                    school.Capacity,
                    school.Quality,
                    (Skill)school.Emphasis,
                    new Money(school.MonthlyCostCents),
                    owner)
                {
                    Enrolled = school.Enrolled
                };
                var entity = world!.Create(component);
                var networkId = new NetworkId(school.NetworkId);
                world.Add(entity, networkId);
                networkIds.AllocateWithValue(entity, networkId);
                schoolEntities[school.NetworkId] = entity;
            }

            var restoredTick = state.Tick;
            foreach (var sim in state.Sims)
            {
                var stage = Enum.TryParse<LifeStage>(sim.Stage, out var savedStage)
                    ? savedStage
                    : LifecycleClassifier.Classify(sim.Age, economy.Life.Policy);
                var ageTicks = sim.AgeTicks > 0
                    ? sim.AgeTicks
                    : LifecycleFactory.Create(sim.Age, restoredTick, clock, economy.Life.Policy, sim.NetworkId.GetHashCode()).AgeTicks;
                var cause = Enum.TryParse<StageTransitionCause>(sim.StageCause, out var savedCause)
                    ? savedCause
                    : StageTransitionCause.Restored;
                var lastAgedTick = !state.LifecycleMigrated && sim.LastAgedTick == 0
                    ? restoredTick
                    : sim.LastAgedTick;

                var profile = new SkillProfile
                {
                    Programming = sim.SkillProgramming,
                    Art = sim.SkillArt,
                    Cooking = sim.SkillCooking,
                    Medical = sim.SkillMedical,
                    Engineering = sim.SkillEngineering,
                    Sales = sim.SkillSales
                };

                var spawnTile = ResolveRestoredTile(sim);
                var (spawnX, spawnY) = mapContext is not null
                    ? mapContext.Options.ToWorld(spawnTile)
                    : (sim.X, sim.Y);

                var entity = world!.Create(
                    new GenName(sim.Name),
                    new Needs { Hunger = sim.Hunger, Energy = sim.Energy, Hygiene = sim.Hygiene, Bladder = sim.Bladder },
                    new CurrentAction(
                        Enum.TryParse<ActionType>(sim.ActionType, out var at) ? at : ActionType.Idle,
                        sim.TimeRemaining),
                    new Alive(sim.Age),
                    new Location(spawnX, spawnY),
                    profile,
                    new Health(sim.HealthValue <= 0f ? HealthSystem.InitialForAge(sim.Age) : sim.HealthValue)
                    {
                        DeclineRate = sim.HealthDeclineRate,
                        LastCareTick = sim.HealthLastCareTick,
                        ChronicConditions = sim.ChronicConditions
                    },
                    new Lifecycle(stage, ageTicks, sim.StageEnteredTick, cause, lastAgedTick)
                    {
                        MaxAgeFlagged = sim.MaxAgeFlagged
                    }
                );

                var walletCents = sim.WalletBalanceCents != 0
                    ? sim.WalletBalanceCents
                    : (long)Math.Round((decimal)sim.WalletBalance * 100m);
                if (walletCents != 0 || sim.EmployerNetworkId != 0)
                    world.Add(entity, new Wallet(new Money(walletCents)));

                if (sim.SavingsBalanceCents != 0)
                    world.Add(entity, new SavingsAccount(new Money(sim.SavingsBalanceCents)));

                if (sim.UnmetDemandCents != 0 || sim.LastSpendCents != 0 || sim.ConsumptionLifetimeCents != 0)
                {
                    world.Add(entity, new ConsumptionState
                    {
                        UnmetDemand = new Money(sim.UnmetDemandCents),
                        LastCycleSpent = new Money(sim.LastSpendCents),
                        LifetimeSpent = new Money(sim.ConsumptionLifetimeCents),
                        LastSpendTick = state.Tick
                    });
                }

                if (sim.PensionEmployeeCents != 0 || sim.PensionEmployerCents != 0)
                {
                    world.Add(entity, new PensionAccount
                    {
                        EmployeeContributions = new Money(sim.PensionEmployeeCents),
                        EmployerContributions = new Money(sim.PensionEmployerCents),
                        ContributingCycles = sim.PensionCycles
                    });
                }

                var networkId = new NetworkId(sim.NetworkId);
                world.Add(entity, networkId);
                networkIds.AllocateWithValue(entity, networkId);
                simEntities[sim.NetworkId] = entity;

                if (mapContext is not null)
                {
                    world.Add(entity, new TilePosition(spawnTile));
                    if (sim.HasMoveTarget)
                        world.Add(entity, new MoveTarget(new TileCoord(sim.MoveTargetX, sim.MoveTargetY)));

                    var movement = new MovementState(MovementOptions.PlayerSpeedTilesPerSecond);
                    if (sim.MoveProgress > 0f || sim.MoveToX != 0 || sim.MoveToY != 0)
                    {
                        movement.From = new TileCoord(sim.MoveFromX, sim.MoveFromY);
                        movement.To = new TileCoord(sim.MoveToX, sim.MoveToY);
                        movement.Progress = sim.MoveProgress;
                        movement.IsMoving = sim.MoveProgress > 0f;
                    }
                    world.Add(entity, movement);
                }

                if (sim.IsLookingForWork)
                    world.Add(entity, new JobSeeker(
                        true,
                        profile.ToMask(),
                        sim.DesiredSalaryCents > 0 ? new Money(sim.DesiredSalaryCents) : Money.FromUnits(1000m)));

                if (sim.PlayerId != null)
                    world.Add(entity, new PlayerControlled(sim.PlayerId));

                if (sim.EnrollmentSchoolNetworkId != 0 || sim.IsUnenrolled)
                {
                    var schoolEntity = sim.EnrollmentSchoolNetworkId != 0 &&
                                       schoolEntities.TryGetValue(sim.EnrollmentSchoolNetworkId, out var school)
                        ? school
                        : Entity.Null;
                    world.Add(entity, new Enrollment(
                        schoolEntity,
                        sim.StageEnteredTick,
                        sim.EnrollmentAttendance <= 0f ? 0.5f : sim.EnrollmentAttendance,
                        sim.IsUnenrolled || schoolEntity == Entity.Null)
                    {
                        MonthsAttended = sim.EnrollmentMonths
                    });
                }

                if (sim.IsDead)
                {
                    world.Add(entity, new Dead(
                        sim.DeathTick == 0 ? restoredTick : sim.DeathTick,
                        Enum.TryParse<DeathCause>(sim.DeathCause, out var deathCause) ? deathCause : DeathCause.Other));
                }
            }

            foreach (var sim in state.Sims)
            {
                if (!simEntities.TryGetValue(sim.NetworkId, out var entity))
                    continue;
                if (sim.EmployerNetworkId == 0 || sim.SalaryCents <= 0)
                    continue;
                if (!companyEntities.TryGetValue(sim.EmployerNetworkId, out var company))
                    continue;
                officeEntities.TryGetValue(sim.OfficeNetworkId, out var office);

                var employee = new Employee(
                    company,
                    office,
                    sim.Position,
                    new Money(sim.SalaryCents))
                {
                    Seniority = Enum.TryParse<Seniority>(sim.Seniority, out var seniority)
                        ? seniority
                        : Seniority.Associate,
                    HiredAtTick = sim.HiredAtTick
                };
                world!.Add(entity, employee);
                world.Add(entity, new EmploymentRecord(sim.HiredAtTick)
                {
                    Attendance = sim.EmploymentAttendance,
                    Performance = sim.EmploymentPerformance,
                    TenureCycles = sim.EmploymentTenureCycles,
                    Warnings = sim.EmploymentWarnings,
                    ShiftsCompleted = sim.EmploymentShiftsCompleted,
                    ShiftsExpected = sim.EmploymentShiftsExpected,
                    Interruptions = sim.EmploymentInterruptions,
                    ConsecutiveLowMonths = sim.EmploymentConsecutiveLowMonths,
                    HighPerformanceMonths = sim.EmploymentHighPerformanceMonths,
                    LastReviewTick = sim.EmploymentLastReviewTick
                });
                if (sim.WorkOfficeNetworkId != 0 &&
                    officeEntities.TryGetValue(sim.WorkOfficeNetworkId, out var workOffice))
                {
                    world.Add(entity, new WorkSchedule(workOffice)
                    {
                        IsAtWork = sim.IsAtWork,
                        ShiftTicksRemaining = sim.ShiftTicksRemaining,
                        LastShiftStartTick = sim.LastShiftStartTick,
                        LastShiftEndTick = sim.LastShiftEndTick,
                        ShiftsCompleted = sim.WorkShiftsCompleted,
                        ShiftsInterrupted = sim.WorkShiftsInterrupted
                    });
                }
                else
                {
                    world.Add(entity, new WorkSchedule(office));
                }
                employmentIndex!.Add(company, entity);
            }

            foreach (var sim in state.Sims)
            {
                if (!simEntities.TryGetValue(sim.NetworkId, out var entity))
                    continue;
                if (sim.ClaimCompanyNetworkId == 0)
                    continue;
                if (!companyEntities.TryGetValue(sim.ClaimCompanyNetworkId, out var claimCompany))
                    continue;
                if (sim.UnpaidNetCents == 0 && sim.UnpaidGrossCents == 0 && sim.SeveranceCents == 0)
                    continue;

                world!.Add(entity, new WageClaim(
                    claimCompany,
                    new Money(sim.UnpaidGrossCents),
                    new Money(sim.UnpaidNetCents),
                    sim.ClaimFirstTick)
                {
                    Severance = new Money(sim.SeveranceCents),
                    CyclesInArrears = sim.ClaimCycles
                });
            }

            foreach (var sim in state.Sims)
            {
                if (!simEntities.TryGetValue(sim.NetworkId, out var entity))
                    continue;

                if (sim.RetirementMonthlyCents != 0)
                {
                    world!.Add(entity, new RetirementIncome(
                        new Money(sim.RetirementMonthlyCents),
                        sim.RetirementStartedTick,
                        sim.RetirementFund,
                        sim.RetirementMeansTested)
                    {
                        Reduced = sim.RetirementReduced
                    });
                }

                if (sim.UnemploymentMaxCycles > 0 || sim.UnemploymentBenefitCents != 0)
                {
                    world.Add(entity, new UnemploymentRecord(
                        sim.UnemploymentStartedTick,
                        new Money(sim.UnemploymentBenefitCents),
                        sim.UnemploymentMaxCycles,
                        sim.UnemploymentEligible)
                    {
                        CyclesClaimed = sim.UnemploymentCyclesClaimed,
                        UnpaidCycles = sim.UnemploymentUnpaidCycles
                    });
                }
            }

            foreach (var application in state.Applications)
            {
                if (!simEntities.TryGetValue(application.ApplicantNetworkId, out var applicant))
                    continue;
                if (!postingEntities.TryGetValue(application.PostingNetworkId, out var posting))
                    continue;
                companyEntities.TryGetValue(application.CompanyNetworkId, out var company);

                var component = new JobApplication(applicant, posting, application.Position, company, application.MatchScore)
                {
                    ApplicationDate = application.ApplicationDate,
                    Status = Enum.TryParse<ApplicationStatus>(application.Status, out var status)
                        ? status
                        : ApplicationStatus.Pending
                };
                world!.Create(component);
            }

            foreach (var loan in state.Loans)
            {
                if (!companyEntities.TryGetValue(loan.CompanyNetworkId, out var company))
                    continue;
                var model = new Loan
                {
                    LoanId = loan.LoanId,
                    Company = company,
                    CompanyNetworkId = loan.CompanyNetworkId,
                    Principal = new Money(loan.PrincipalCents),
                    Outstanding = new Money(loan.OutstandingCents),
                    AnnualRate = loan.AnnualRate,
                    TermCycles = loan.TermCycles,
                    CyclesRemaining = loan.CyclesRemaining,
                    PaymentPerCycle = new Money(loan.PaymentPerCycleCents),
                    MissedPayments = loan.MissedPayments,
                    IssuedAtTick = loan.IssuedAtTick,
                    State = Enum.TryParse<LoanState>(loan.State, out var loanState)
                        ? loanState
                        : LoanState.Active
                };
                economy!.Loans.Add(model);
            }

            var propertyEntities = new Dictionary<ulong, Entity>();
            var unitEntities = new Dictionary<ulong, Entity>();
            var householdEntities = new Dictionary<ulong, Entity>();

            foreach (var household in state.Households)
            {
                if (!simEntities.TryGetValue(household.HeadNetworkId, out var head))
                    continue;

                var entity = world!.Create(new Household
                {
                    Head = head,
                    Unit = Entity.Null,
                    Tenure = Enum.TryParse<Tenure>(household.Tenure, out var tenure)
                        ? tenure
                        : Tenure.Renter,
                    MonthlyRent = new Money(household.MonthlyRentCents),
                    MoveInTick = household.MoveInTick,
                    ArrearsCycles = household.ArrearsCycles,
                    RentArrears = new Money(household.RentArrearsCents),
                    UtilityArrears = new Money(household.UtilityArrearsCents),
                    Debt = new Money(household.DebtCents),
                    LastRentTick = household.LastRentTick,
                    LastUtilityTick = household.LastUtilityTick,
                    NoticeStage = household.NoticeStage,
                    NoticeStartedTick = household.NoticeStartedTick,
                    CureDeadlineTick = household.CureDeadlineTick,
                    LastMoveTick = household.LastMoveTick,
                    OverburdenMonths = household.OverburdenMonths,
                    NoUtilities = household.NoUtilities,
                    MemberCount = household.MemberCount
                });
                var networkId = new NetworkId(household.NetworkId);
                world.Add(entity, networkId);
                networkIds.AllocateWithValue(entity, networkId);
                householdEntities[household.NetworkId] = entity;
            }

            Entity ResolveHousingOwner(ulong networkId)
            {
                if (networkId == 0)
                    return Entity.Null;
                if (companyEntities.TryGetValue(networkId, out var company))
                    return company;
                if (householdEntities.TryGetValue(networkId, out var household))
                    return household;
                return Entity.Null;
            }

            foreach (var property in state.Properties)
            {
                var entity = world!.Create(new Property(
                    property.Name,
                    property.District,
                    ResolveHousingOwner(property.OwnerNetworkId),
                    property.UnitCount,
                    property.Condition,
                    property.Neighbourhood,
                    new Money(property.MonthlyUpkeepCents),
                    property.IsShelter));
                var networkId = new NetworkId(property.NetworkId);
                world.Add(entity, networkId);
                networkIds.AllocateWithValue(entity, networkId);
                propertyEntities[property.NetworkId] = entity;
            }

            foreach (var unit in state.HousingUnits)
            {
                if (!propertyEntities.TryGetValue(unit.PropertyNetworkId, out var property))
                    continue;

                var entity = world!.Create(new HousingUnit(
                    property,
                    unit.UnitNumber,
                    Enum.TryParse<HousingTier>(unit.Tier, out var tier)
                        ? tier
                        : HousingTier.Basic,
                    unit.Capacity,
                    new Money(unit.BaseRentCents),
                    unit.Condition,
                    unit.IsVacant,
                    ResolveHousingOwner(unit.OwnerNetworkId),
                    new Money(unit.PurchasePriceCents))
                {
                    OccupantCount = unit.OccupantCount,
                    IsCondemned = unit.IsCondemned
                });
                var networkId = new NetworkId(unit.NetworkId);
                world.Add(entity, networkId);
                networkIds.AllocateWithValue(entity, networkId);
                unitEntities[unit.NetworkId] = entity;
            }

            foreach (var household in state.Households)
            {
                if (!householdEntities.TryGetValue(household.NetworkId, out var entity))
                    continue;
                var data = world!.Get<Household>(entity);
                if (household.UnitNetworkId != 0 &&
                    unitEntities.TryGetValue(household.UnitNetworkId, out var unit))
                    data.Unit = unit;
                world!.Get<Household>(entity) = data;
            }

            foreach (var member in state.HouseholdMembers)
            {
                if (!householdEntities.TryGetValue(member.HouseholdNetworkId, out var household))
                    continue;
                if (!simEntities.TryGetValue(member.SimNetworkId, out var sim))
                    continue;
                if (world!.Has<HouseholdMember>(sim))
                    continue;

                world.Add(sim, new HouseholdMember(
                    household,
                    Enum.TryParse<HouseholdRole>(member.Role, out var role)
                        ? role
                        : HouseholdRole.Other));
            }

            foreach (var project in state.ConstructionProjects)
            {
                var entity = world!.Create(new ConstructionProject
                {
                    ProjectId = project.ProjectId,
                    Developer = ResolveHousingOwner(project.DeveloperNetworkId),
                    District = project.District,
                    Tier = Enum.TryParse<HousingTier>(project.Tier, out var tier)
                        ? tier
                        : HousingTier.Basic,
                    Units = project.Units,
                    Cost = new Money(project.CostCents),
                    PaidToDate = new Money(project.PaidToDateCents),
                    MonthsRemaining = project.MonthsRemaining,
                    TotalMonths = project.TotalMonths,
                    StartedAtTick = project.StartedAtTick
                });
                var networkId = new NetworkId(project.ProjectId);
                world.Add(entity, networkId);
                networkIds.AllocateWithValue(entity, networkId);
            }

            RestorePlacements(state, propertyEntities, officeEntities, schoolEntities);
            EmitMapGenerated(state.Tick);
            EmitAllMapBuildings(state.Tick);
            RebuildMovementRequests();

            economy!.Housing.Rebuild(world!);

            world!.Query(in new QueryDescription().WithAll<ConstructionProject, NetworkId>(),
                (Entity entity, ref ConstructionProject _, ref NetworkId nid) =>
                    economy.Housing.AttachNetworkId(entity, nid));

            var market = state.HousingMarket ?? new PersistedHousingMarket();
            economy.Housing.Market.Index = market.Index;
            economy.Housing.Market.LastUpdatedTick = market.LastUpdatedTick;
            economy.Housing.Market.EvictionsThisYear = market.EvictionsThisYear;
            economy.Housing.Market.MovesThisYear = market.MovesThisYear;
            economy.Housing.Market.CompletionsThisYear = market.CompletionsThisYear;
            economy.Housing.Market.RentCollectedThisMonth = new Money(market.RentCollectedThisMonthCents);
            economy.Housing.Market.UtilityCollectedThisMonth = new Money(market.UtilityCollectedThisMonthCents);
            economy.Housing.Market.UpkeepPaidThisMonth = new Money(market.UpkeepPaidThisMonthCents);
            economy.Housing.Market.RentCollectedThisYear = new Money(market.RentCollectedThisYearCents);
            economy.Housing.Market.UtilityCollectedThisYear = new Money(market.UtilityCollectedThisYearCents);
            economy.Housing.Market.UpkeepPaidThisYear = new Money(market.UpkeepPaidThisYearCents);
            foreach (var district in market.Districts)
            {
                economy.Housing.Market.DistrictCapacity[district.Name] = district.Capacity;
                economy.Housing.Market.DistrictUnits[district.Name] = district.Units;
            }

            foreach (var snapshot in state.HousingMetricHistory)
            {
                economy.Housing.MetricHistory.Add(new HousingSnapshot(
                    snapshot.Tick,
                    snapshot.Year,
                    snapshot.Properties,
                    snapshot.Units,
                    snapshot.OccupiedUnits,
                    snapshot.VacantUnits,
                    snapshot.ShelterBeds,
                    snapshot.ShelteredHouseholds,
                    snapshot.HomelessHouseholds,
                    snapshot.OvercrowdedUnits,
                    snapshot.UnitsBelowCondition,
                    snapshot.VacancyRate,
                    snapshot.RentIndex,
                    new Money(snapshot.RentRollCents),
                    new Money(snapshot.RentCollectedCents),
                    new Money(snapshot.RentArrearsCents),
                    new Money(snapshot.UtilityArrearsCents),
                    new Money(snapshot.UpkeepPaidCents),
                    snapshot.EvictionsThisYear,
                    snapshot.ConstructionUnits,
                    snapshot.MedianRentBurden,
                    snapshot.ShelterOccupancy));
            }

            if (!state.HousingMigrated && state.Households.Count == 0)
            {
                var assigner = new HouseholdAssigner(world!, networkIds, economy);
                assigner.AssignExistingHomeless(state.RandomSeed, state.Tick);
            }

            economy.Life.Immigration.TargetPopulation = state.Immigration.TargetPopulation;
            economy.Life.Immigration.Enabled = state.Immigration.Enabled;
            economy.Life.ImmigrationState.ArrivalsThisYear = state.Immigration.ArrivalsThisYear;
            economy.Life.ImmigrationState.ArrivalBudget = state.Immigration.ArrivalBudget;
            economy.Life.ImmigrationState.Year = state.Immigration.Year;
            economy.Life.ImmigrationState.CancelledThisYear = state.Immigration.CancelledThisYear;
            economy.Life.ImmigrationState.Paused = state.Immigration.Paused;
            foreach (var pending in state.Immigration.Pending)
            {
                economy.Life.ImmigrationState.Pending.Add(new PendingArrival
                {
                    Name = pending.Name,
                    Age = pending.Age,
                    QueuedTick = pending.QueuedTick,
                    ExpiresTick = pending.ExpiresTick,
                    Profile = new SkillProfile
                    {
                        Programming = pending.SkillProgramming,
                        Art = pending.SkillArt,
                        Cooking = pending.SkillCooking,
                        Medical = pending.SkillMedical,
                        Engineering = pending.SkillEngineering,
                        Sales = pending.SkillSales
                    }
                });
            }

            foreach (var row in state.MortalityLog)
            {
                economy.Life.MortalityLog.Add(new MortalityRecord(
                    row.Tick,
                    row.SimNetworkId,
                    row.Name,
                    row.Age,
                    Enum.TryParse<DeathCause>(row.Cause, out var deathCause) ? deathCause : DeathCause.Other,
                    row.Wealth));
            }

            foreach (var row in state.LifecycleMetricHistory)
            {
                economy.Life.MetricHistory.Add(new PopulationSnapshot(
                    row.Tick,
                    row.Year,
                    row.Total,
                    row.Education,
                    row.Work,
                    row.Retirement,
                    row.Elderly,
                    row.StudentsEnrolled,
                    row.GraduatesThisYear,
                    row.Employed,
                    row.Unemployed,
                    row.RetiredThisYear,
                    row.DeathsThisYear,
                    row.ArrivalsThisYear,
                    row.AverageAge,
                    row.MedianAge,
                    row.AverageHealth,
                    row.AverageAgeAtDeath,
                    row.LifeExpectancy,
                    row.PensionReplacementRate,
                    row.PensionFundingRatio,
                    row.WorkingAgeShare,
                    row.OldAgeDependencyRatio,
                    row.PyramidBuckets,
                    row.DeathCauses.Select(cause => new DeathCauseCount(cause.Cause, cause.Count)).ToList()));
            }

            if (!state.LifecycleMigrated)
            {
                foreach (var sim in state.Sims)
                {
                    if (!simEntities.TryGetValue(sim.NetworkId, out var entity) || sim.IsDead)
                        continue;

                    var stage = economy.Life.Policy.StageFor(sim.Age);
                    if (stage != LifeStage.Work && world!.Has<Employee>(entity))
                    {
                        LifeSystemHelpers.RemoveEmployment(
                            world,
                            employmentIndex!,
                            economy,
                            entity,
                            state.Tick);
                    }

                    if (stage != LifeStage.Work && world.Has<JobSeeker>(entity))
                        world.Remove<JobSeeker>(entity);
                    if (stage != LifeStage.Work && world.Has<UnemploymentRecord>(entity))
                        world.Remove<UnemploymentRecord>(entity);

                    if (stage is LifeStage.Retirement or LifeStage.Elderly &&
                        !world.Has<RetirementIncome>(entity))
                    {
                        var account = world.Has<PensionAccount>(entity)
                            ? world.Get<PensionAccount>(entity)
                            : default;
                        var (pension, meansTested) = economy.Life.Pensions.EntitlementFor(
                            account,
                            economy.Life.Policy);
                        world.Add(entity, new RetirementIncome(
                            pension,
                            state.Tick,
                            meansTested ? "means-tested" : "statement",
                            meansTested));
                    }

                    if (stage == LifeStage.Work && !world.Has<Employee>(entity) &&
                        !world.Has<JobSeeker>(entity))
                    {
                        var profile = world.Has<SkillProfile>(entity)
                            ? world.Get<SkillProfile>(entity)
                            : new SkillProfile();
                        world.Add(entity, new JobSeeker(
                            true,
                            profile.ToMask(),
                            economy.Life.Careers.SalaryForSkills(profile.Total)));
                    }
                }
            }

            var estateRequests = new List<EstateRequest>();
            var queuedDeaths = new HashSet<ulong>();
            foreach (var row in state.EstateQueue)
            {
                if (!simEntities.TryGetValue(row.SimNetworkId, out var deadSim) ||
                    !world!.IsAlive(deadSim))
                    continue;

                Entity household = Entity.Null;
                Entity unit = Entity.Null;
                Entity ownedUnit = Entity.Null;
                if (row.HouseholdNetworkId != 0)
                    householdEntities.TryGetValue(row.HouseholdNetworkId, out household);
                if (row.UnitNetworkId != 0)
                    unitEntities.TryGetValue(row.UnitNetworkId, out unit);
                if (row.OwnedUnitNetworkId != 0)
                    unitEntities.TryGetValue(row.OwnedUnitNetworkId, out ownedUnit);

                var heirs = new List<Entity>();
                foreach (var heirId in row.Heirs)
                {
                    if (simEntities.TryGetValue(heirId, out var heir) && world.IsAlive(heir))
                        heirs.Add(heir);
                }

                estateRequests.Add(new EstateRequest
                {
                    Sim = deadSim,
                    Household = household,
                    Unit = unit,
                    OwnedUnit = ownedUnit,
                    Tick = row.Tick,
                    Name = row.Name,
                    SimNetworkId = row.SimNetworkId,
                    Age = row.Age,
                    Cause = Enum.TryParse<DeathCause>(row.Cause, out var estateCause)
                        ? estateCause
                        : DeathCause.Other,
                    Heirs = heirs,
                    HouseholdRentArrears = row.HouseholdRentArrears,
                    HouseholdUtilityArrears = row.HouseholdUtilityArrears
                });
                queuedDeaths.Add(row.SimNetworkId);
            }

            foreach (var sim in state.Sims.Where(sim => sim.IsDead))
            {
                if (queuedDeaths.Contains(sim.NetworkId) ||
                    !simEntities.TryGetValue(sim.NetworkId, out var deadSim) ||
                    !world!.IsAlive(deadSim))
                    continue;

                economy.Life.DeathQueue.Add(deadSim);
            }

            if (economy.Life.DeathQueue.Count > 0)
                new DeathProcessingSystem(world!, economy).Update(state.Tick);

            economy.Life.EstateQueue.AddRange(estateRequests);
            if (economy.Life.EstateQueue.Count > 0)
                new EstateSettlementSystem(world!, economy).Update(state.Tick);

            foreach (var sim in state.Sims.Where(sim => sim.IsDead))
            {
                if (simEntities.TryGetValue(sim.NetworkId, out var deadSim) && world!.IsAlive(deadSim))
                    economy.Life.QueueDestroy(deadSim, restoredTick + 1);
            }

            economy.Life.Index.Rebuild(world!);

            if (state.LifecycleMigrated)
                new StageProgressionSystem(world!, economy).Update(state.Tick, clock);

            economy.Life.Index.Rebuild(world!);

            world!.Query(in new QueryDescription().WithAll<Alive, Lifecycle>().WithNone<Dead>(),
                (Entity entity, ref Alive alive, ref Lifecycle lifecycle) =>
                {
                    if (lifecycle.Stage != economy.Life.Policy.StageFor(alive.Age))
                        throw new InvalidOperationException(
                            "Lifecycle restore mismatch for sim " +
                            $"{GetNetworkId(world!, entity)}: {lifecycle.Stage} at age {alive.Age}");
                });

            state.LifecycleMigrated = true;

            var lifecycleIssues = LifecycleInvariants.Verify(world!, economy.Life.Policy);
            if (lifecycleIssues.Count > 0)
                throw new InvalidOperationException("Lifecycle restore validation failed: " +
                                                    string.Join("; ", lifecycleIssues));

            var issues = economy.Housing.Verify();
            if (issues.Count > 0)
                throw new InvalidOperationException(
                    "Housing restore verification failed: " + string.Join("; ", issues.Take(10)));

            state.HousingMigrated = true;

            economy!.Government.Balance = new Money(state.Fiscal.GovernmentBalanceCents);
            economy.Government.IncomeTaxCollected = new Money(state.Fiscal.IncomeTaxCollectedCents);
            economy.Government.CorporateTaxCollected = new Money(state.Fiscal.CorporateTaxCollectedCents);
            economy.Government.BenefitsPaid = new Money(state.Fiscal.BenefitsPaidCents);
            economy.Government.OfficeCostsCollected = new Money(state.Fiscal.OfficeCostsCollectedCents);
            economy.Pension.Balance = new Money(state.Fiscal.PensionBalanceCents);
            economy.Pension.ContributionsThisCycle = new Money(state.Fiscal.PensionContributionsCents);
            economy.Bank.Balance = new Money(state.Fiscal.BankBalanceCents);
            economy.Bank.InterestPool = new Money(state.Fiscal.BankInterestPoolCents);
            economy.WagesPaidThisYear = new Money(state.Fiscal.WagesPaidThisYearCents);
            economy.RevenueThisYear = new Money(state.Fiscal.RevenueThisYearCents);
            economy.TaxThisYear = new Money(state.Fiscal.TaxThisYearCents);
            economy.BasicIncomeThisYear = new Money(state.Fiscal.BasicIncomeThisYearCents);
            economy.ConsumptionThisYear = new Money(state.Fiscal.ConsumptionThisYearCents);

            foreach (var snapshot in state.MetricHistory)
            {
                economy!.MetricHistory.Add(new Genelife.Economy.EconomySnapshot(
                    snapshot.Tick,
                    snapshot.Year,
                    new Money(snapshot.HouseholdCashCents),
                    new Money(snapshot.HouseholdSavingsCents),
                    new Money(snapshot.CompanyCashCents),
                    new Money(snapshot.CompanyDebtCents),
                    new Money(snapshot.PensionFundCents),
                    new Money(snapshot.GovernmentFundCents),
                    new Money(snapshot.BankFundsCents),
                    new Money(snapshot.TotalMoneySupplyCents),
                    new Money(snapshot.WagesPaidCents),
                    new Money(snapshot.RevenueCents),
                    new Money(snapshot.TaxCents),
                    new Money(snapshot.BasicIncomeCents),
                    new Money(snapshot.ArrearsCents),
                    snapshot.UnemploymentRate,
                    snapshot.VacancyRate,
                    snapshot.WageShare,
                    snapshot.ProfitShare,
                    snapshot.Gini,
                    snapshot.HomelessHouseholds,
                    snapshot.BankruptCompanies));
            }

            foreach (var pending in state.PendingRevenue)
            {
                var amount = new Money(pending.AmountCents);
                if (companyEntities.TryGetValue(pending.CompanyNetworkId, out var company))
                {
                    economy.BookRevenue(company, pending.Category, pending.SimNetworkId, amount, pending.Tick);
                }
                else
                {
                    economy.Government.Balance += amount;
                }
            }

            tick = state.Tick;
        }
    }
}