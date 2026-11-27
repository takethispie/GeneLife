using System.Diagnostics;
using System.Collections.Concurrent;
using System.Threading.Channels;
using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Api.Appearance;
using Genelife.Api.Consumption;
using Genelife.Api.DTOs;
using Genelife.Api.Social;
using Genelife.Appearance;
using Genelife.Api.Persistence;
using Genelife.Api.Snapshots;
using Genelife.Api.Commands;
using Genelife.Api.Repositories;
using Genelife.Components.Company;
using Genelife.Components.Consumption;
using Genelife.Consumption;
using Genelife.Components.Employment;
using Genelife.Components.Family;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Components.Map;
using Genelife.Components.Player;
using Genelife.Components.Social;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Family;
using Genelife.Housing;
using Genelife.Life;
using Genelife.Map;
using Genelife.Messages.Events;
using Genelife.Player;
using Genelife.Social;
using Genelife.Application.Systems;
using Genelife.Values;

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
    private RelationshipOptions relationshipOptions = RelationshipOptions.Default;
    private RelationshipIndex relationshipIndex = new();
    private RelationshipService? relationshipService;
    private Genelife.Family.PlayerRelationshipService? playerRelationships;
    private Genelife.Social.VenueService? venueService;
    private Genelife.Social.VenueActivityService? venueActivities;
    private RelationshipCache relationshipCache = new();
    private ReproductionOptions reproductionOptions = ReproductionOptions.Default;
    private FamilyHousingPolicy familyHousingPolicy = FamilyHousingPolicy.Default;
    private PregnancyIndex pregnancyIndex = new();
    private PregnancyService? pregnancyService;
    private NewbornFactory? newbornFactory;
    private InheritanceOptions inheritanceOptions = InheritanceOptions.Default;
    private GuardianshipOptions guardianshipOptions = GuardianshipOptions.Default;
    private TrustService? trustService;
    private GuardianshipService? guardianshipService;
    private SystemRegistry? systems;
    private bool isRunning;
    private long tick;
    private WorldSnapshot? _latest;
    // Appearance is a pure function of traits, age band and version. Snapshot
    // construction is the only writer, and it runs under the world lock (tick)
    // or the snapshot consumer's read lock, so no extra synchronisation is
    // needed. Cleared on Initialize/Shutdown.
    private readonly Dictionary<Entity, AppearanceCacheEntry> appearanceCache = new();
    // Reader/writer lock: the tick holds the write lock while it mutates the
    // world, and read-only accessors take the read lock so HTTP and broadcast
    // readers do not serialize against each other or against snapshot builds.
    private readonly ReaderWriterLockSlim @lock = new(LockRecursionPolicy.SupportsRecursion);

    private WriteLockScope WriteLock() => new(@lock);

    private ReadLockScope ReadLock() => new(@lock);

    private readonly struct WriteLockScope : IDisposable
    {
        private readonly ReaderWriterLockSlim gate;

        public WriteLockScope(ReaderWriterLockSlim gate)
        {
            this.gate = gate;
            gate.EnterWriteLock();
        }

        public void Dispose() => gate.ExitWriteLock();
    }

    private readonly struct ReadLockScope : IDisposable
    {
        private readonly ReaderWriterLockSlim gate;

        public ReadLockScope(ReaderWriterLockSlim gate)
        {
            this.gate = gate;
            gate.EnterReadLock();
        }

        public void Dispose() => gate.ExitReadLock();
    }
private readonly NetworkIdRegistry networkIds = new();
    private readonly SnapshotHistory history = new();
    private Thread? snapshotThread;
    private CancellationTokenSource? snapshotCancellation;
    private AutoResetEvent? snapshotSignal;
    private int snapshotPending;
    private readonly Action<SystemRunResult> recordSystemRun;
    private readonly ConcurrentQueue<EnqueuedCommand> queuedCommands = new();
    private readonly ConcurrentQueue<AdminCommand> adminCommands = new();
    private readonly ConcurrentQueue<IntentRejection> intentRejections = new();
    private readonly ConcurrentQueue<PlayerLifecycleNotice> playerLifecycleNotices = new();
    private readonly ConcurrentDictionary<Guid, CommandResult> commandResults = new();
    private readonly ConcurrentDictionary<Guid, byte> seenAdminCommands = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<CommandResult>> adminWaiters = new();
    private readonly Channel<object> outboundEvents = Channel.CreateBounded<object>(
        new BoundedChannelOptions(50_000)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = true
        });
    private long droppedEventCount;
    private readonly List<object> tickEvents = new(4096);
    private readonly HashSet<Guid> processedMessageIds = new(1024);
    private readonly PlayerRegistry playerRegistry = new();
    private PlayerActionService? playerActions;
    private readonly ConcurrentDictionary<Guid, PlayerCommandResult> playerCommandResults = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<PlayerCommandResult>> playerCommandAudit =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> pendingPlayerCommands = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> disconnectedPlayers = new(StringComparer.Ordinal);
    private const int MaxPendingPlayerCommands = 8;
    private const int MaxAuditRowsPerPlayer = 50;
    private readonly IEventConsumerProbe? eventConsumerProbe;
    private readonly IWorldPersistence? worldPersistence;
    private readonly Genelife.Social.SocialGraphService socialGraph;
    private readonly Genelife.Social.VenueOptions venueOptions;
    private readonly Genelife.Api.Auth.IConnectionRegistry? connections;
    private readonly Genelife.Social.SocialPrivacyRegistry? socialPrivacy;
    private static readonly string[] ActionNames = Enum.GetNames<ActionType>();

    public SimulationEngine(
        IWorldPersistence? worldPersistence = null,
        IEventConsumerProbe? eventConsumerProbe = null,
        ICommandQueue? commandQueue = null,
        Genelife.Social.SocialGraphService? socialGraph = null,
        Genelife.Social.VenueOptions? venueOptions = null,
        Genelife.Api.Auth.IConnectionRegistry? connections = null,
        Genelife.Social.SocialPrivacyRegistry? socialPrivacy = null)
    {
        this.worldPersistence = worldPersistence;
        this.eventConsumerProbe = eventConsumerProbe;
        this.commandQueue = commandQueue;
        this.socialGraph = socialGraph ?? new Genelife.Social.SocialGraphService();
        this.venueOptions = venueOptions ?? new Genelife.Social.VenueOptions();
        this.venueOptions.Validate();
        this.connections = connections;
        this.socialPrivacy = socialPrivacy;
        recordSystemRun = result =>
            metrics.RecordSystem(result.Name, result.Milliseconds, result.AllocatedBytes);
    }

    public bool IsRunning { get { using (ReadLock()) { return isRunning; } } }
    public World? World { get { using (ReadLock()) { return world; } } }
    public EconomyContext? Economy { get { using (ReadLock()) { return economy; } } }
    public SimulationClock Clock => clock;
    public SimulationClockOptions ClockOptions { get; set; } = new();
    public Genelife.Map.MapOptions MapOptions { get; set; } = Genelife.Map.MapOptions.Default;
    public Genelife.Map.MapGenerationOptions MapGenerationOptions { get; set; } = Genelife.Map.MapGenerationOptions.Default;
    public Genelife.Map.MovementOptions MovementOptions { get; set; } = Genelife.Map.MovementOptions.Default;
    public Genelife.Map.CommuteOptions CommuteOptions { get; set; } = Genelife.Map.CommuteOptions.Default;
    public MapContext? MapContext { get { using (ReadLock()) { return mapContext; } } }
    public MovementIndex? MovementIndex { get { using (ReadLock()) { return movementIndex; } } }
    public IReadOnlyList<MapMetricsSnapshot> MapMetricsHistory => mapMetrics?.History ?? Array.Empty<MapMetricsSnapshot>();
    public int MapVersion { get { using (ReadLock()) { return mapContext?.Version ?? 0; } } }
    public int RandomSeed { get; set; } = 1;
    public EmploymentIndex? EmploymentIndex { get { using (ReadLock()) { return employmentIndex; } } }
    public RelationshipIndex Relationships { get { using (ReadLock()) { return relationshipIndex; } } }
    public RelationshipService? RelationshipService { get { using (ReadLock()) { return relationshipService; } } }
    public Genelife.Social.SocialGraphService SocialGraph => socialGraph;
    public Genelife.Family.PlayerRelationshipService? PlayerRelationships { get { using (ReadLock()) { return playerRelationships; } } }
    public Genelife.Social.VenueService? Venues { get { using (ReadLock()) { return venueService; } } }
    public Genelife.Social.VenueActivityService? VenueActivities { get { using (ReadLock()) { return venueActivities; } } }
    public RelationshipOptions RelationshipOptions { get { using (ReadLock()) { return relationshipOptions; } } }
    public RelationshipCache RelationshipCache { get { using (ReadLock()) { return relationshipCache; } } }
    public ReproductionOptions ReproductionOptions { get { using (ReadLock()) { return reproductionOptions; } } }
    public FamilyHousingPolicy FamilyHousingPolicy { get { using (ReadLock()) { return familyHousingPolicy; } } }
    public PregnancyIndex Pregnancies { get { using (ReadLock()) { return pregnancyIndex; } } }
    public PregnancyService? PregnancyService { get { using (ReadLock()) { return pregnancyService; } } }
    public InheritanceOptions InheritanceOptions { get { using (ReadLock()) { return inheritanceOptions; } } }
    public GuardianshipOptions GuardianshipOptions { get { using (ReadLock()) { return guardianshipOptions; } } }
    public TrustService? Trusts { get { using (ReadLock()) { return trustService; } } }
    public GuardianshipService? Guardianship { get { using (ReadLock()) { return guardianshipService; } } }
    public NetworkIdRegistry NetworkIds => networkIds;
    public PlayerRegistry Players => playerRegistry;
    public SnapshotHistory SnapshotHistory => history;
    public long LastSnapshotBuildTimeMs { get; private set; }
    public double LastTickMilliseconds { get; private set; }
    public long CurrentTick { get { using (ReadLock()) { return tick; } } }
    public bool SnapshotsEnabled { get; set; } = true;
    /// <summary>
    /// Spreads monthly and pay-cycle systems across a small phase window. Must be
    /// set before <see cref="Initialize"/>. Off by default: staggering changes the
    /// order in which coupled family and payroll passes see the world, which
    /// moves the decade balance bands even though per-interval run counts and
    /// same-seed determinism are unchanged. Enable it for measurement runs.
    /// </summary>
    public bool StaggerCadence { get; set; }
    /// <summary>
    /// When true the tick signals a dedicated consumer instead of building the
    /// snapshot inline. The hosted simulation loop enables this so snapshot
    /// construction stays off the tick budget. Tests that call Tick directly
    /// leave it false and get a synchronous snapshot.
    /// </summary>
    public bool AsyncSnapshots { get; set; }

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

    /// <summary>
    /// Real-time player death/respawn notices for the hub transport. Unlike the
    /// persisted event stream these are best-effort and scoped to one deployment.
    /// </summary>
    public bool TryDequeuePlayerLifecycleNotice(out PlayerLifecycleNotice notice) =>
        playerLifecycleNotices.TryDequeue(out notice!);

    private void RejectIntent(string playerId, long sequence, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            return;

        intentRejections.Enqueue(new IntentRejection(playerId, sequence, code, message));
    }

    public void Initialize(
        EconomyOptions? economyOptions = null,
        HousingOptions? housingOptions = null,
        GeneticsOptions? geneticsOptions = null,
        RelationshipOptions? relationshipOptions = null,
        ReproductionOptions? reproductionOptions = null,
        FamilyHousingPolicy? familyHousingPolicy = null,
        InheritanceOptions? inheritanceOptions = null,
        GuardianshipOptions? guardianshipOptions = null,
        long startTick = 0)
    {
        // Do not tear down a running engine's snapshot consumer just because a
        // second Initialize call arrives; the guarded block below returns early.
        if (IsRunning)
            return;

        if (!@lock.IsWriteLockHeld)
            StopSnapshotConsumer();

        using (WriteLock())
        {
            if (isRunning) return;
            var stagger = StaggerCadence ? 1 : 0;
            world = World.Create();
            mapContext = MapContext.Generate(world, MapOptions, MapGenerationOptions);
            movementIndex = new MovementIndex();
            mapMetrics = new MapMetricsSystem(world, mapContext.Map);
            emittedMapBuildings.Clear();
            emittedMapVersion = mapContext.Version;
            EmitMapGenerated(startTick);
            lastIntentSequence.Clear();
            metrics = new Metrics();
            appearanceCache.Clear();
            employmentIndex = new EmploymentIndex();
            clock = new SimulationClock(ClockOptions);
            economy = new EconomyContext(
                world,
                employmentIndex,
                options: economyOptions,
                clock: clock,
                randomSeed: RandomSeed,
                housingOptions: housingOptions,
                geneticsOptions: geneticsOptions);
            economy.Housing.AttachNetworkIdAllocator(entity => networkIds.Allocate(entity));
            economy.Life.AttachNetworkIdAllocator(entity => networkIds.Allocate(entity));

            this.relationshipOptions = relationshipOptions ?? RelationshipOptions.Default;
            this.relationshipOptions.Validate();
            relationshipIndex = new RelationshipIndex();
            relationshipCache = new RelationshipCache();
            relationshipIndex.NetworkIdResolver = entity =>
                world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : (ulong)entity.Id;
            relationshipService = new RelationshipService(world, relationshipIndex)
            {
                NetworkIdAllocator = entity => networkIds.Allocate(entity),
                NetworkIdRemover = entity => networkIds.Remove(entity),
                EdgeCreated = (edge, relationship) => economy?.Emit(new RelationshipFormed(
                    GetNetworkId(world, edge),
                    GetNetworkId(world, relationship.From),
                    GetNetworkId(world, relationship.To),
                    relationship.Kind.ToString(),
                    relationship.Source.ToString(),
                    relationship.SinceTick)),
                EdgeChanged = (edge, relationship) => economy?.Emit(new RelationshipFormed(
                    GetNetworkId(world, edge),
                    GetNetworkId(world, relationship.From),
                    GetNetworkId(world, relationship.To),
                    relationship.Kind.ToString(),
                    relationship.Source.ToString(),
                    relationship.SinceTick)),
                EdgeRemoved = (edge, relationship) =>
                {
                    var edgeId = GetNetworkId(world, edge);
                    var fromId = GetNetworkId(world, relationship.From);
                    var toId = GetNetworkId(world, relationship.To);
                    if (edgeId == 0 || fromId == 0 || toId == 0)
                        return;
                    economy?.Emit(new RelationshipEnded(
                        edgeId, fromId, toId, relationship.Kind.ToString(), tick));
                }
            };

            this.reproductionOptions = reproductionOptions ?? ReproductionOptions.Default;
            this.reproductionOptions.Validate();
            this.familyHousingPolicy = familyHousingPolicy ?? FamilyHousingPolicy.Default;
            this.familyHousingPolicy.Validate();
            pregnancyIndex = new PregnancyIndex();
            pregnancyIndex.AttachWorld(world);
            pregnancyService = new PregnancyService(world, pregnancyIndex)
            {
                NetworkIdAllocator = entity => networkIds.Allocate(entity),
                NetworkIdRemover = entity => networkIds.Remove(entity)
            };
            newbornFactory = new NewbornFactory(
                world,
                economy,
                relationshipService,
                entity => networkIds.Allocate(entity));
            this.inheritanceOptions = inheritanceOptions ?? InheritanceOptions.Default;
            this.inheritanceOptions.Validate();
            this.guardianshipOptions = guardianshipOptions ?? GuardianshipOptions.Default;
            this.guardianshipOptions.Validate();
            trustService = new TrustService(world, economy, relationshipIndex)
            {
                NetworkIdAllocator = entity => networkIds.Allocate(entity),
                NetworkIdRemover = entity => networkIds.Remove(entity)
            };
            guardianshipService = new GuardianshipService(
                world,
                economy,
                relationshipIndex,
                relationshipService!,
                this.guardianshipOptions);

            playerRegistry.Clear();
            playerCommandResults.Clear();
            playerCommandAudit.Clear();
            pendingPlayerCommands.Clear();
            disconnectedPlayers.Clear();
            playerRelationships = new Genelife.Family.PlayerRelationshipService(
                world,
                economy,
                playerRegistry,
                relationshipIndex,
                relationshipService!,
                socialGraph,
                this.relationshipOptions);
            venueService = new Genelife.Social.VenueService(
                world,
                playerRegistry,
                economy,
                venueOptions,
                connections is null ? null : playerId => connections.GetConnections(playerId).Count > 0);
            venueActivities = new Genelife.Social.VenueActivityService(
                world,
                economy,
                playerRegistry,
                venueService,
                socialGraph);
            playerActions = new PlayerActionService(
                world,
                economy,
                networkIds,
                playerRegistry,
                relationshipIndex,
                relationshipService!,
                pregnancyIndex,
                pregnancyService!,
                this.relationshipOptions,
                this.reproductionOptions,
                playerRelationships,
                venueService,
                venueActivities);

            systems = new SystemRegistry(samplingInterval: 5);
            systems.Register(new NeedsDecaySystem(world));
            systems.Register(new PathfindingSystem(world, mapContext.Map, movementIndex, MovementOptions.PathRequestBudgetPerTick, MovementOptions.MaxPathIterationsPerTick));
            systems.Register(new MovementSystem(world, mapContext.Map, movementIndex, MovementOptions.PlayerSpeedTilesPerSecond));
            systems.Register(new VenuePresenceSystem(venueService));
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
            systems.Register(new ConceptionSystem(world, economy, relationshipIndex, pregnancyIndex, pregnancyService, this.reproductionOptions, cadenceOffset: 2 * stagger));
            systems.Register(new PregnancySystem(world, economy, pregnancyIndex, pregnancyService, this.reproductionOptions, cadenceOffset: 4 * stagger));
            systems.Register(new BirthSystem(world, economy, pregnancyIndex, pregnancyService, newbornFactory, entity => networkIds.Allocate(entity), this.reproductionOptions, cadenceOffset: 6 * stagger));
            systems.Register(new FamilyHouseholdSystem(world, economy, relationshipIndex, this.relationshipOptions, this.familyHousingPolicy, cadenceOffset: 8 * stagger));
            systems.Register(new TrustSystem(economy, trustService, cadenceOffset: 12 * stagger));
            systems.Register(new StateCareSystem(world, economy, cadenceOffset: 10 * stagger));
            systems.Register(new RelationshipExposureSystem(world, economy, relationshipIndex, this.relationshipOptions, relationshipCache, cadenceOffset: 0));
            systems.Register(new AttractionSystem(world, economy, relationshipIndex, this.relationshipOptions, relationshipCache, cadenceOffset: 3 * stagger));
            systems.Register(new PairingSystem(world, economy, relationshipService!, relationshipIndex, this.relationshipOptions, relationshipCache, cadenceOffset: 6 * stagger));
            systems.Register(new StageProgressionSystem(world, economy));
            systems.Register(new HiringSystem(world, employmentIndex, 25, economy));
            systems.Register(new PensionSystem(world, economy));
            systems.Register(new UnemploymentBenefitSystem(world, economy));
            systems.Register(new ElderDeclineSystem(world, economy));
            systems.Register(new MortalitySystem(world, economy));
            systems.Register(new NeedsDeathSystem(world, economy));
            systems.Register(new DeathProcessingSystem(
                world,
                economy,
                relationships: relationshipService,
                relationshipIndex: relationshipIndex,
                guardianship: guardianshipService,
                inheritanceOptions: this.inheritanceOptions,
                trusts: trustService,
                pregnancies: pregnancyService));
            systems.Register(new EstateSettlementSystem(
                world,
                economy,
                relationshipIndex: relationshipIndex,
                relationshipService: relationshipService,
                inheritanceOptions: this.inheritanceOptions,
                trusts: trustService));
            systems.Register(new PopulationInflowSystem(world, economy, mapContext));
            systems.Register(new PopulationMetricsSystem(world, economy, relationships: relationshipIndex));
            systems.Register(new CompanyRevenueSystem(world, economy));
            systems.Register(new ConsumerSpendingSystem(world, economy));
            systems.Register(new PayrollSystem(world, economy, cadenceOffset: 9 * stagger));
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
            systems.Register(new JobGrowthSystem(world, networkIds, economy, employmentIndex, mapContext));
            systems.Register(new JobSeekerSystem(world, 100, economy));
            systems.Register(new HousingDevelopmentSystem(world, economy, mapContext));
            systems.Register(new EconomyMetricsSystem(world, economy));
            systems.Register(new HousingMetricsSystem(world, economy));
            systems.Register(new UtilityConsumptionSystem(
                world,
                economy,
                entity => networkIds.AllocateInfrastructure(entity)));
            systems.Register(new UtilityOutageSystem(world, economy));
            systems.Register(new GroceryStockSystem(
                world,
                economy,
                entity => networkIds.AllocateInfrastructure(entity)));
            systems.Register(new PlayerLifecycleSystem(
                world,
                economy,
                playerRegistry,
                relationshipIndex,
                this.inheritanceOptions,
                CreateContinuationGenos));
            systems.Register(new PlayerEmploymentSystem(world, economy));
            systems.Register(new PlayerHouseholdSystem(world, economy));
            systems.Register(new PlayerConsumptionSystem(world, economy));
            systems.Register(new PlayerLifeMetricsSystem(world, economy, playerRegistry));
            systems.Register(new NutritionSystem(world, economy));
            systems.Register(new ConsumptionMetricsSystem(world, economy));

            networkIds.Clear();
            history.Clear();
            tick = startTick;
            isRunning = true;
        }
    }

    public void Shutdown()
    {
        if (!@lock.IsWriteLockHeld)
            StopSnapshotConsumer();

        IWorldSaveBatch? batchToSave = null;

        using (WriteLock())
        {
            if (!isRunning)
                return;

            if (world != null && employmentIndex != null && worldPersistence != null)
                batchToSave = worldPersistence.Build(world, employmentIndex);

            metrics.Flush();
            world?.Dispose();
            world = null;
            economy = null;
            mapContext = null;
            movementIndex = null;
            mapMetrics = null;
            employmentIndex = null;
            relationshipService = null;
            relationshipCache.Clear();
            relationshipIndex.Clear();
            pregnancyService = null;
            pregnancyIndex.Clear();
            newbornFactory = null;
            trustService = null;
            guardianshipService = null;
            playerActions = null;
            playerRegistry.Clear();
            playerCommandResults.Clear();
            playerCommandAudit.Clear();
            pendingPlayerCommands.Clear();
            disconnectedPlayers.Clear();
            systems = null;
            history.Clear();
            networkIds.Clear();
            _latest = null;
            appearanceCache.Clear();
            isRunning = false;
        }

        if (batchToSave != null && worldPersistence != null)
        {
            worldPersistence.SaveBatchAsync(batchToSave).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Captures a consistent world save batch under the simulation lock. The caller
    /// persists it off the tick thread.
    /// </summary>
    public IWorldSaveBatch? PrepareSave()
    {
        using (ReadLock())
        {
            if (!isRunning || world == null || employmentIndex == null || worldPersistence == null)
                return null;

            return worldPersistence.Build(world, employmentIndex);
        }
    }

    public void Tick(float deltaTime)
    {
        var events = tickEvents;
        events.Clear();

        DrainCommands();
        DrainAdminCommands();
        DrainPlayerIntents();

        var tickStart = Stopwatch.GetTimestamp();
        var allocatedStart = GC.GetAllocatedBytesForCurrentThread();
        var hasEventConsumer = eventConsumerProbe?.HasConsumer == true;

        // Phase 1: mutate the world under the write lock only.
        World currentWorld;
        using (WriteLock())
        {
            if (!isRunning || world == null)
                return;

            tick++;
            currentWorld = world;

            var context = new SimulationContext(tick, deltaTime, clock);
            economy.Tick = tick;

            if (systems != null)
                systems.Update(in context, recordSystemRun);

            if (economy is not null)
            {
                foreach (var e in economy.DrainEvents())
                {
                    events.Add(e);
                    ObservePlayerLifecycleEvent(currentWorld, e, events);
                }
            }
        }

        // Phase 2: read the world under the read lock. Snapshot construction and
        // event collection no longer block request readers behind the write lock.
        using (ReadLock())
        {
            // Collecting genos/map events is a full-world pass that only the
            // persistence writer consumes. Skip it entirely when nobody reads
            // the outbound channel.
            if (hasEventConsumer)
            {
                CollectGenosEvents(currentWorld, events);
                CollectMapEvents(events);
            }

            if (SnapshotsEnabled)
            {
                if (AsyncSnapshots)
                {
                    SignalSnapshotConsumer();
                }
                else
                {
                    PublishSnapshot(currentWorld);
                }
            }
        }

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedStart;
        LastTickMilliseconds = ElapsedMilliseconds(tickStart);
        metrics.RecordTick(LastTickMilliseconds, allocatedBytes, currentWorld.Size);

        if (hasEventConsumer)
            events.Add(new TickCompleted(tick, currentWorld.Size, ElapsedMilliseconds(tickStart)));

        if (hasEventConsumer)
        {
            foreach (var e in events)
            {
                if (!outboundEvents.Writer.TryWrite(e))
                    Interlocked.Increment(ref droppedEventCount);
            }
        }
    }

    /// <summary>
    /// Builds and publishes a world snapshot. The caller must hold at least the
    /// read lock so the world cannot change underneath the enumeration.
    /// </summary>
    private void PublishSnapshot(World currentWorld)
    {
        var snapshotStart = Stopwatch.GetTimestamp();
        var genoses = BuildGenosSnapshots(currentWorld);
        var companies = BuildCompanySnapshots(currentWorld, employmentIndex!);
        var next = new WorldSnapshot(tick, WorldSnapshot.CurrentVersion, genoses, companies);
        LastSnapshotBuildTimeMs = (long)ElapsedMilliseconds(snapshotStart);

        Interlocked.Exchange(ref _latest, next);
        history.Record(next);

        metrics.RecordSystem("SnapshotBuild", LastSnapshotBuildTimeMs, 0);
    }

    private void SignalSnapshotConsumer()
    {
        EnsureSnapshotConsumer();
        var signal = snapshotSignal;
        if (signal is not null && Interlocked.Exchange(ref snapshotPending, 1) == 0)
            signal.Set();
    }

    private void EnsureSnapshotConsumer()
    {
        if (snapshotThread is not null)
            return;

        snapshotCancellation = new CancellationTokenSource();
        var signal = new AutoResetEvent(false);
        snapshotSignal = signal;
        snapshotThread = new Thread(() => SnapshotConsumerLoop(signal, snapshotCancellation.Token))
        {
            IsBackground = true,
            Name = "genelife-snapshot-consumer"
        };
        snapshotThread.Start();
    }

    /// <summary>
    /// Builds the world snapshot off the tick thread. Signals are coalesced: if
    /// the tick publishes several times while a build is in flight, the consumer
    /// builds the latest world once and moves on.
    /// </summary>
    private void SnapshotConsumerLoop(AutoResetEvent signal, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                signal.WaitOne(100);
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            if (cancellationToken.IsCancellationRequested)
                return;

            if (Interlocked.Exchange(ref snapshotPending, 0) == 0)
                continue;

            try
            {
                using (ReadLock())
                {
                    if (!isRunning || world is null || !SnapshotsEnabled)
                        continue;

                    PublishSnapshot(world);
                }
            }
            catch (Exception)
            {
                // Snapshot construction must never take down the consumer; the
                // next tick will publish again.
            }
        }
    }

    private void StopSnapshotConsumer()
    {
        var cancellation = snapshotCancellation;
        var signal = snapshotSignal;
        var thread = snapshotThread;

        snapshotCancellation = null;
        snapshotSignal = null;
        snapshotThread = null;

        if (cancellation is null || thread is null)
            return;

        cancellation.Cancel();
        try
        {
            signal?.Set();
        }
        catch (ObjectDisposedException)
        {
        }

        if (thread != Thread.CurrentThread)
            thread.Join(TimeSpan.FromSeconds(5));

        signal?.Dispose();
        cancellation.Dispose();
        Interlocked.Exchange(ref snapshotPending, 0);
    }

    private void ObservePlayerLifecycleEvent(World currentWorld, object @event, List<object> events)
    {
        if (@event is GenosDied died &&
            networkIds.TryGetEntity(new NetworkId(died.GenosNetworkId), out var dead) &&
            currentWorld.IsAlive(dead))
        {
            PlayerProfile? profile = null;
            if (playerRegistry.TryGetByGenos(dead, out var byGenos))
                profile = byGenos;
            else if (playerRegistry.TryGetByLastGenosNetworkId(died.GenosNetworkId, out var byLast))
                profile = byLast;

            if (profile is not null)
            {
                profile.LastGenosName ??= died.Name;
                profile.LastGenosAge = profile.LastGenosAge == 0 ? died.Age : profile.LastGenosAge;
                if (profile.LastGenosTraits is null && currentWorld.Has<Traits>(dead))
                    profile.LastGenosTraits = currentWorld.Get<Traits>(dead);
                if (profile.LastGenosHouseholdNetworkId == 0)
                    profile.LastGenosHouseholdNetworkId = died.HouseholdNetworkId;
                if (profile.LastGenosDeathTick == 0)
                    profile.LastGenosDeathTick = died.Tick;
            }

            return;
        }

        if (@event is PlayerAwaitingContinuation awaiting)
        {
            if (awaiting.CanRespawn &&
                playerRegistry.TryGet(awaiting.PlayerId, out var profile))
            {
                profile.LastGenosName ??= awaiting.Name;
                profile.LastGenosAge = profile.LastGenosAge == 0 ? awaiting.Age : profile.LastGenosAge;

                // The monthly estate pass removes the dead genos's relationship
                // edges. Create the respawn child after that pass but while the
                // dead entity still exists so the parent edge survives.
                if (profile.ContinuationPolicy == PlayerContinuationPolicy.Respawn &&
                    profile.PendingRespawnGenosNetworkId == 0 &&
                    networkIds.TryGetEntity(new NetworkId(awaiting.GenosNetworkId), out var deadEntity) &&
                    currentWorld.IsAlive(deadEntity) &&
                    currentWorld.Has<Dead>(deadEntity))
                {
                    TryCreatePendingRespawnChild(
                        currentWorld,
                        profile,
                        deadEntity,
                        awaiting.GenosNetworkId,
                        profile.LastGenosHouseholdNetworkId,
                        awaiting.Tick,
                        events);
                }
            }

            playerLifecycleNotices.Enqueue(new PlayerLifecycleNotice(
                awaiting.PlayerId,
                "died",
                awaiting.GenosNetworkId,
                0,
                awaiting.Name,
                awaiting.Age,
                awaiting.Cause,
                awaiting.Tick,
                awaiting.CanRespawn));
            return;
        }

        if (@event is PlayerContinuationApplied applied)
        {
            playerLifecycleNotices.Enqueue(new PlayerLifecycleNotice(
                applied.PlayerId,
                "respawned",
                applied.NewGenosNetworkId,
                applied.PreviousGenosNetworkId,
                applied.Name,
                0,
                null,
                applied.Tick,
                false));
        }
    }

    /// <summary>
    /// For a Respawn-policy player, create the child immediately at death. The
    /// dead entity is still alive here, which is the only reliable window for
    /// linking the parent edge; the player later takes control of this genos.
    /// </summary>
    private void TryCreatePendingRespawnChild(
        World currentWorld,
        PlayerProfile profile,
        Entity dead,
        ulong genosNetworkId,
        ulong householdNetworkId,
        long tick,
        List<object> events)
    {
        if (profile.ContinuationPolicy != PlayerContinuationPolicy.Respawn ||
            profile.Observer ||
            profile.PendingRespawnGenosNetworkId != 0 ||
            newbornFactory is null ||
            economy is null)
        {
            return;
        }

        var household = Entity.Null;
        if (householdNetworkId != 0 &&
            networkIds.TryGetEntity(new NetworkId(householdNetworkId), out var householdEntity) &&
            currentWorld.IsAlive(householdEntity) &&
            economy.Housing.MemberCount(householdEntity) > 0)
        {
            household = householdEntity;
        }

        var parentTraits = profile.LastGenosTraits;
        if (parentTraits is null && currentWorld.Has<Traits>(dead))
            parentTraits = currentWorld.Get<Traits>(dead);

        var age = economy.Life.Policy.WorkStartAge;
        var child = newbornFactory.CreateChild(dead, parentTraits, household, age, tick);
        var childNetworkId = currentWorld.Has<NetworkId>(child.Entity)
            ? currentWorld.Get<NetworkId>(child.Entity).Value
            : 0;

        if (mapContext is not null)
            PlayerSpawnService.PlaceAt(currentWorld, child.Entity, economy.Housing, mapContext, childNetworkId);
        if (!currentWorld.Has<MovementState>(child.Entity))
            currentWorld.Add(child.Entity, new MovementState(MovementOptions.PlayerSpeedTilesPerSecond));

        profile.PendingRespawnGenosNetworkId = childNetworkId;
        events.Add(new ChildBorn(
            childNetworkId,
            genosNetworkId,
            0,
            householdNetworkId,
            tick));
        events.Add(new BirthRecorded(childNetworkId, 0, child.Name, tick));
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

    private void CollectGenosEvents(World world, List<object> events)
    {        var query = new QueryDescription().WithAll<Needs, NetworkId>();
        world.Query(in query, (ref Needs needs, ref NetworkId nid) =>
        {
            events.Add(new GenosNeedsChanged(nid.Value, needs.Hunger, needs.Energy, needs.Hygiene, needs.Bladder));
        });
    }

    private void DrainCommands()
    {
        while (queuedCommands.TryDequeue(out var command))
        {
            if (!processedMessageIds.Add(command.MessageId))
                continue;

            using (WriteLock())
            {
                if (!isRunning || world == null)
                    continue;

                switch (command.Type)
                {
                    case CommandType.CreateGenos:
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
                                    MapSpawner.PlaceGenos(world, entity, mapContext, nid.Value);
                                economy.Life.Index.OnBorn(economy.Life.Policy.StageFor(age));
                            }
                        }
                        break;

                    case CommandType.DeleteGenos:
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

    /// <summary>
    /// Enqueues an admin mutation and blocks until the simulation tick applies
    /// it. Legacy admin endpoints need the created network id synchronously;
    /// routing through the queue keeps the request thread out of the world.
    /// </summary>
    public CommandResult ExecuteAdminCommandSync(AdminCommand command, TimeSpan? timeout = null)
    {
        if (!isRunning)
            throw new InvalidOperationException("Simulation is not running");

        var waiter = new TaskCompletionSource<CommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        adminWaiters[command.CommandId] = waiter;
        EnqueueAdminCommand(command);

        // A duplicate command id may already have a result; complete immediately
        // instead of waiting for a drain that will not happen.
        if (commandResults.TryGetValue(command.CommandId, out var completed))
        {
            adminWaiters.TryRemove(command.CommandId, out _);
            return completed;
        }

        var wait = timeout ?? TimeSpan.FromSeconds(2);
        if (!waiter.Task.Wait(wait))
        {
            adminWaiters.TryRemove(command.CommandId, out _);
            throw new TimeoutException(
                $"Admin command {command.CommandId} was not applied within {wait.TotalMilliseconds:F0} ms.");
        }

        return waiter.Task.Result;
    }

    public bool TryReservePlayerCommand(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            return false;

        while (true)
        {
            if (!pendingPlayerCommands.TryGetValue(playerId, out var current))
            {
                if (pendingPlayerCommands.TryAdd(playerId, 1))
                    return true;
                continue;
            }

            if (current >= MaxPendingPlayerCommands)
                return false;
            if (pendingPlayerCommands.TryUpdate(playerId, current + 1, current))
                return true;
        }
    }

    public void ReleasePlayerCommand(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
            return;

        while (pendingPlayerCommands.TryGetValue(playerId, out var current))
        {
            if (current <= 1)
            {
                if (pendingPlayerCommands.TryRemove(playerId, out _))
                    return;
            }
            else if (pendingPlayerCommands.TryUpdate(playerId, current - 1, current))
            {
                return;
            }
        }
    }

    public void MarkPlayerConnected(string playerId)
    {
        if (!string.IsNullOrWhiteSpace(playerId))
            disconnectedPlayers.TryRemove(playerId, out _);
    }

    public void MarkPlayerDisconnected(string playerId)
    {
        if (!string.IsNullOrWhiteSpace(playerId))
            disconnectedPlayers[playerId] = 0;
    }

    public PlayerCommandResult? GetPlayerCommandResult(Guid commandId) =>
        playerCommandResults.TryGetValue(commandId, out var result) ? result : null;

    public IReadOnlyList<PlayerCommandResult> GetPlayerCommandAudit(string playerId, int take = 20)
    {
        if (take <= 0 || !playerCommandAudit.TryGetValue(playerId, out var queue))
            return Array.Empty<PlayerCommandResult>();
        return queue.Reverse().Take(Math.Min(take, MaxAuditRowsPerPlayer)).ToArray();
    }

    private void DrainPlayerIntents()
    {
        if (commandQueue is null)
            return;

        commandQueue.Drain(intent =>
        {
            using (WriteLock())
            {
                if (!isRunning || world is null)
                    return;

                if (intent is PlayerActionIntent playerAction)
                {
                    ApplyPlayerAction(playerAction);
                    return;
                }

                if (mapContext is null || movementIndex is null)
                    return;

                if (intent is MoveIntent move)
                    ApplyMoveIntent(move);
                else if (intent is UseBuildingIntent use)
                    ApplyUseBuildingIntent(use);
            }
        });
    }

    private void ApplyPlayerAction(PlayerActionIntent intent)
    {
        if (string.IsNullOrWhiteSpace(intent.PlayerId) || intent.CommandId == Guid.Empty)
            return;

        try
        {
            if (playerCommandResults.ContainsKey(intent.CommandId))
                return;
            if (disconnectedPlayers.ContainsKey(intent.PlayerId))
            {
                CompletePlayerCommand(intent, PlayerCommandStatus.Rejected, "disconnected",
                    "The player disconnected before the action was applied.", applied: false);
                return;
            }

            if (lastIntentSequence.TryGetValue(intent.PlayerId, out var last) &&
                intent.Sequence > 0 && intent.Sequence <= last)
            {
                CompletePlayerCommand(intent, PlayerCommandStatus.Superseded, "stale_sequence",
                    "Intent sequence is stale.", applied: false);
                return;
            }
            if (intent.Sequence > 0)
                lastIntentSequence[intent.PlayerId] = intent.Sequence;

            if (playerActions is null)
            {
                CompletePlayerCommand(intent, PlayerCommandStatus.Rejected, "world_not_ready",
                    "The world is not ready yet.", applied: false);
                return;
            }

            var genos = ResolvePlayerGenos(intent.PlayerId, intent.GenosNetworkId);
            if (genos != Entity.Null && world.Has<NetworkId>(genos))
                intent = intent with { GenosNetworkId = world.Get<NetworkId>(genos).Value };
            var result = playerActions.Apply(intent.PlayerId, genos, intent.Kind, intent.Payload, tick);
            CompletePlayerCommand(
                intent,
                result.Applied ? PlayerCommandStatus.Applied : PlayerCommandStatus.Rejected,
                result.Code,
                result.Message,
                applied: result.Applied);
        }
        finally
        {
            ReleasePlayerCommand(intent.PlayerId);
        }
    }

    private void CompletePlayerCommand(
        PlayerActionIntent intent,
        PlayerCommandStatus status,
        string? code,
        string? message,
        bool applied)
    {
        var result = new PlayerCommandResult(
            intent.CommandId,
            intent.PlayerId,
            intent.Kind,
            status,
            code,
            message,
            tick);

        playerCommandResults[intent.CommandId] = result;
        var queue = playerCommandAudit.GetOrAdd(
            intent.PlayerId,
            _ => new ConcurrentQueue<PlayerCommandResult>());
        queue.Enqueue(result);
        while (queue.Count > MaxAuditRowsPerPlayer && queue.TryDequeue(out _))
        {
        }

        if (applied)
            playerRegistry.RecordCommand();

        economy?.Emit(new PlayerCommandApplied(
            intent.CommandId,
            intent.PlayerId,
            intent.GenosNetworkId,
            intent.Kind.ToString(),
            status.ToString(),
            code,
            tick));

        if (status != PlayerCommandStatus.Applied)
            RejectIntent(intent.PlayerId, intent.Sequence, code ?? "rejected", message ?? "Command rejected.");
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

        var genos = ResolvePlayerGenos(intent.PlayerId, intent.GenosNetworkId);
        if (genos == Entity.Null || !world.IsAlive(genos) || world.Has<Dead>(genos))
        {
            RejectIntent(intent.PlayerId, intent.Sequence, "genos_not_found", "Your genos is not available");
            return;
        }

        var owner = world.Has<PlayerControlled>(genos) ? world.Get<PlayerControlled>(genos).PlayerId : null;
        if (!string.Equals(owner, intent.PlayerId, StringComparison.Ordinal))
        {
            RejectIntent(intent.PlayerId, intent.Sequence, "not_owner", "You do not control that genos");
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

        var current = world.Has<TilePosition>(genos)
            ? world.Get<TilePosition>(genos).Tile
            : mapContext.Options.ToTile(
                world.Has<Location>(genos) ? world.Get<Location>(genos).Position.X : 0f,
                world.Has<Location>(genos) ? world.Get<Location>(genos).Position.Y : 0f);

        if (!mapContext.Options.Bounds.Contains(current) || !mapContext.Map.IsWalkable(current))
        {
            current = MapSpawner.FindSpawn(mapContext, world.Has<NetworkId>(genos) ? world.Get<NetworkId>(genos).Value : 0);
            MapSpawner.PlaceGenosAt(world, genos, mapContext, current);
        }

        if (!world.Has<MovementState>(genos))
            world.Add(genos, new MovementState(MovementOptions.PlayerSpeedTilesPerSecond));

        if (current == target)
        {
            if (world.Has<MoveTarget>(genos))
            {
                var existing = world.Get<MoveTarget>(genos);
                existing.HasTarget = false;
                world.Get<MoveTarget>(genos) = existing;
            }
            movementIndex.Clear(genos);
            return;
        }

        var moveTarget = new MoveTarget(target, tick, movementIndex.PathVersion);
        if (world.Has<MoveTarget>(genos))
            world.Get<MoveTarget>(genos) = moveTarget;
        else
            world.Add(genos, moveTarget);

        movementIndex.RequestPath(genos, current, target, mapContext.Version);
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

        var genos = ResolvePlayerGenos(intent.PlayerId, intent.GenosNetworkId);
        if (genos == Entity.Null || !world.IsAlive(genos) || world.Has<Dead>(genos))
        {
            RejectIntent(intent.PlayerId, intent.Sequence, "genos_not_found", "Your genos is not available");
            return;
        }
        var owner = world.Has<PlayerControlled>(genos) ? world.Get<PlayerControlled>(genos).PlayerId : null;
        if (!string.Equals(owner, intent.PlayerId, StringComparison.Ordinal))
        {
            RejectIntent(intent.PlayerId, intent.Sequence, "not_owner", "You do not control that genos");
            return;
        }

        var placement = world.Get<Placement>(building);
        if (world.Get<TilePosition>(genos).Tile.ChebyshevDistanceTo(placement.Entry) > 1)
        {
            RejectIntent(intent.PlayerId, intent.Sequence, "out_of_range", "Walk to the entrance first");
            return;
        }

        if (world.Has<SocialVenue>(building))
        {
            if (venueService is null)
            {
                RejectIntent(intent.PlayerId, intent.Sequence, "venue_unavailable", "The venue is not available");
                return;
            }

            if (!venueService.TryEnter(intent.PlayerId, building, tick, out var venueCode, out var venueMessage))
            {
                RejectIntent(
                    intent.PlayerId,
                    intent.Sequence,
                    venueCode.Length == 0 ? "venue_unavailable" : venueCode,
                    venueMessage.Length == 0 ? "The venue is not available" : venueMessage);
                return;
            }

            if (world.Has<MoveTarget>(genos))
            {
                var moveTarget = world.Get<MoveTarget>(genos);
                moveTarget.HasTarget = false;
                world.Get<MoveTarget>(genos) = moveTarget;
            }
            movementIndex?.Clear(genos);
            return;
        }

        ActionType? action = null;
        if (world.Has<Property>(building))
        {
            action = ActionType.Sleeping;
        }
        else if (world.Has<Office>(building))
        {
            if (!world.Has<Employee>(genos) || world.Get<Employee>(genos).Office != building)
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

        var current = world.Has<CurrentAction>(genos)
            ? world.Get<CurrentAction>(genos)
            : new CurrentAction(ActionType.Idle, 0f);
        current = new CurrentAction(action.Value, action.Value == ActionType.Studying ? 60f : 0f);
        if (world.Has<CurrentAction>(genos))
            world.Get<CurrentAction>(genos) = current;
        else
            world.Add(genos, current);

        if (action.Value == ActionType.Working)
            playerActions?.RecordWorkPresence(genos, tick);

        if (!world.Has<MoveTarget>(genos))
            return;
        var target = world.Get<MoveTarget>(genos);
        target.HasTarget = false;
        world.Get<MoveTarget>(genos) = target;
        movementIndex?.Clear(genos);
    }

    private Entity ResolvePlayerGenos(string playerId, ulong networkId)    {
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

    public ulong GetPlayerGenosNetworkId(string playerId)
    {
        using (ReadLock())
        {
            if (world is null || string.IsNullOrWhiteSpace(playerId))
                return 0;
            var genos = ResolvePlayerGenos(playerId, 0);
            return genos != Entity.Null && world.IsAlive(genos) && world.Has<NetworkId>(genos)
                ? world.Get<NetworkId>(genos).Value
                : 0;
        }
    }

    public TileCoord? GetPlayerTile(string playerId)
    {
        using (ReadLock())
        {
            if (world is null || mapContext is null || string.IsNullOrWhiteSpace(playerId))
                return null;

            var genos = ResolvePlayerGenos(playerId, 0);
            if (genos == Entity.Null || !world.IsAlive(genos) || world.Has<Dead>(genos))
                return null;

            if (world.Has<TilePosition>(genos))
                return world.Get<TilePosition>(genos).Tile;

            if (world.Has<Location>(genos))
            {
                var location = world.Get<Location>(genos).Position;
                return mapContext.Options.ToTile(location.X, location.Y);
            }

            return null;
        }
    }

    /// <summary>
    /// Creates a player-controlled genos for a new browser player when the world is
    /// running and the player does not already own a living genos. Returns the
    /// player's genos network id, or 0 when no genos could be created.
    /// </summary>
    public ulong EnsurePlayerGenos(string playerId)
    {
        using (WriteLock())
        {
            if (!isRunning || world is null || string.IsNullOrWhiteSpace(playerId))
                return 0;

            if (playerRegistry.TryGet(playerId, out var known))
            {
                if (known.AwaitingContinuation || known.Observer)
                    return 0;

                // A reconnect can arrive after death but before the lifecycle
                // system's next cadence. Never mint a replacement genos here.
                if (known.ControlledGenos != Entity.Null &&
                    world.IsAlive(known.ControlledGenos) &&
                    world.Has<Dead>(known.ControlledGenos))
                {
                    known.AwaitingContinuation = true;
                    known.LastGenosNetworkId = world.Has<NetworkId>(known.ControlledGenos)
                        ? world.Get<NetworkId>(known.ControlledGenos).Value
                        : 0;
                    if (world.Has<GenName>(known.ControlledGenos))
                        known.LastGenosName = world.Get<GenName>(known.ControlledGenos).Name;
                    if (world.Has<Alive>(known.ControlledGenos))
                        known.LastGenosAge = world.Get<Alive>(known.ControlledGenos).Age;
                    if (known.LastGenosTraits is null && world.Has<Traits>(known.ControlledGenos))
                        known.LastGenosTraits = world.Get<Traits>(known.ControlledGenos);
                    return 0;
                }
            }

            var existing = ResolvePlayerGenos(playerId, 0);
            if (existing != Entity.Null && world.IsAlive(existing) && !world.Has<Dead>(existing))
            {
                RegisterPlayerGenos(playerId, existing);
                return world.Has<NetworkId>(existing) ? world.Get<NetworkId>(existing).Value : 0;
            }

            var entity = CreateContinuationGenos(playerId);
            if (entity is null)
                return 0;

            return world.Has<NetworkId>(entity.Value) ? world.Get<NetworkId>(entity.Value).Value : 0;
        }
    }

    /// <summary>
    /// Creates a grown child of the player's last genos, transfers control to it,
    /// and returns the new identity. Only valid while the profile is awaiting a
    /// continuation decision.
    /// </summary>
    public PlayerRespawnStatus RespawnPlayerAsChild(string playerId)
    {
        using (WriteLock())
        {
            if (!isRunning || world is null || economy is null || newbornFactory is null)
                return RespawnFailed("world_not_ready", "The world is not ready yet.");
            if (string.IsNullOrWhiteSpace(playerId) || !playerRegistry.TryGet(playerId, out var profile))
                return RespawnFailed("player_not_found", "Player not found.");
            if (profile.Observer || profile.ContinuationPolicy == PlayerContinuationPolicy.Observer)
                return RespawnFailed("observer", "Observers cannot respawn.");
            if (!profile.AwaitingContinuation)
                return RespawnFailed("not_awaiting", "Your genos is still alive.");

            var parent = Entity.Null;
            if (profile.LastGenosNetworkId != 0 &&
                networkIds.TryGetEntity(new NetworkId(profile.LastGenosNetworkId), out var parentEntity) &&
                world.IsAlive(parentEntity))
            {
                parent = parentEntity;
            }

            if (parent != Entity.Null && !world.Has<Dead>(parent))
                return RespawnFailed("not_dead", "The previous genos is still alive.");

            // The normal path: a child was created at death while the parent
            // entity was still alive, so the family edge already exists.
            if (profile.PendingRespawnGenosNetworkId != 0 &&
                networkIds.TryGetEntity(new NetworkId(profile.PendingRespawnGenosNetworkId), out var pending) &&
                world.IsAlive(pending) &&
                !world.Has<Dead>(pending))
            {
                return AttachRespawnChild(playerId, profile, pending);
            }

            profile.PendingRespawnGenosNetworkId = 0;

            if (profile.LastGenosName is null && profile.LastGenosTraits is null && parent == Entity.Null)
                return RespawnFailed("no_parent", "No previous genos is available to continue.");

            if (profile.LastGenosName is null && parent != Entity.Null && world.Has<GenName>(parent))
                profile.LastGenosName = world.Get<GenName>(parent).Name;
            if (profile.LastGenosAge == 0 && parent != Entity.Null && world.Has<Alive>(parent))
                profile.LastGenosAge = world.Get<Alive>(parent).Age;

            var household = Entity.Null;
            if (profile.LastGenosHouseholdNetworkId != 0 &&
                networkIds.TryGetEntity(new NetworkId(profile.LastGenosHouseholdNetworkId), out var householdEntity) &&
                world.IsAlive(householdEntity) &&
                economy.Housing.MemberCount(householdEntity) > 0)
            {
                household = householdEntity;
            }

            var parentTraits = profile.LastGenosTraits;
            if (parentTraits is null && parent != Entity.Null && world.Has<Traits>(parent))
                parentTraits = world.Get<Traits>(parent);

            var age = economy.Life.Policy.WorkStartAge;
            var child = newbornFactory.CreateChild(parent, parentTraits, household, age, tick);
            var childNetworkId = world.Has<NetworkId>(child.Entity)
                ? world.Get<NetworkId>(child.Entity).Value
                : 0;

            economy.Emit(new ChildBorn(
                childNetworkId,
                profile.LastGenosNetworkId,
                0,
                household != Entity.Null ? GetNetworkId(world, household) : 0,
                tick));
            economy.Emit(new BirthRecorded(childNetworkId, 0, child.Name, tick));

            return AttachRespawnChild(playerId, profile, child.Entity);
        }
    }

    private PlayerRespawnStatus AttachRespawnChild(
        string playerId,
        PlayerProfile profile,
        Entity child)
    {
        var childNetworkId = world!.Has<NetworkId>(child)
            ? world.Get<NetworkId>(child).Value
            : 0;
        var childName = world.Has<GenName>(child) ? world.Get<GenName>(child).Name : null;
        var age = world.Has<Alive>(child) ? world.Get<Alive>(child).Age : 0;

        if (mapContext is not null)
            PlayerSpawnService.PlaceAt(world, child, economy!.Housing, mapContext, childNetworkId);
        if (!world.Has<MovementState>(child))
            world.Add(child, new MovementState(MovementOptions.PlayerSpeedTilesPerSecond));

        if (world.Has<PlayerControlled>(child))
            world.Get<PlayerControlled>(child) = new PlayerControlled(playerId);
        else
            world.Add(child, new PlayerControlled(playerId));

        var previousGenosId = profile.LastGenosNetworkId;
        profile.Observer = false;
        playerRegistry.AttachGenos(profile, child);
        profile.LastGenosNetworkId = childNetworkId;
        profile.PendingRespawnGenosNetworkId = 0;
        profile.LastGenosName = null;
        profile.LastGenosAge = 0;
        profile.LastGenosTraits = null;
        profile.LastGenosHouseholdNetworkId = 0;
        profile.LastGenosDeathTick = 0;
        playerRegistry.RecordContinuation();

        economy!.Emit(new PlayerContinuationApplied(
            playerId,
            previousGenosId,
            childNetworkId,
            "child",
            null,
            tick,
            childName));

        return new PlayerRespawnStatus(
            true, null, null, childNetworkId, previousGenosId, childName, age);
    }

    private static PlayerRespawnStatus RespawnFailed(string code, string message) =>
        new(false, code, message, 0, 0, null, 0);

    private Entity? CreateContinuationGenos(string playerId)
    {
        if (!isRunning || world is null || economy is null)
            return null;

        var repository = new GenosRepository(world, networkIds, economy, mapContext);
        var name = playerId.Length > 20 ? $"Player-{playerId[..20]}" : $"Player-{playerId}";
        var networkId = repository.Add(name, 25, playerId);
        Emit(new GenosCreated(networkId.Value, name, 25));

        if (!networkIds.TryGetEntity(networkId, out var entity) || !world.IsAlive(entity))
            return null;

        RegisterPlayerGenos(playerId, entity);
        return entity;
    }

    private void RegisterPlayerGenos(string playerId, Entity entity)
    {
        if (world is null || !world.IsAlive(entity))
            return;

        var isNew = !playerRegistry.TryGet(playerId, out _);
        var profile = playerRegistry.GetOrCreate(playerId, entity, tick);
        profile.LastGenosNetworkId = world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : 0;
        profile.Observer = false;
        disconnectedPlayers.TryRemove(playerId, out _);

        if (isNew)
            Emit(new PlayerProfileCreated(profile.LastGenosNetworkId, playerId, tick));
    }

    public void EnsurePlayerSpawn(string playerId)
    {
        using (WriteLock())
        {
            if (world is null || economy is null || mapContext is null)
                return;

            var genos = ResolvePlayerGenos(playerId, 0);
            if (genos == Entity.Null || !world.IsAlive(genos))
                return;

            RegisterPlayerGenos(playerId, genos);

            if (world.Has<TilePosition>(genos) && mapContext.Map.IsWalkable(world.Get<TilePosition>(genos).Tile))
                return;

            var networkId = world.Has<NetworkId>(genos) ? world.Get<NetworkId>(genos).Value : 0;
            PlayerSpawnService.PlaceAt(world, genos, economy.Housing, mapContext, networkId);
            if (!world.Has<MovementState>(genos))
                world.Add(genos, new MovementState(MovementOptions.PlayerSpeedTilesPerSecond));
        }
    }

    /// <summary>
    /// Builds schools, companies, housing and the requested population on the
    /// current map. Shared by the admin population command and the world reset so
    /// both paths behave identically.
    /// </summary>
    public PopulationGenerationResult GeneratePopulation(
        int totalGenoses,
        int seed = 1,
        int? adults = null,
        int? children = null)
    {
        using (WriteLock())
        {
            if (!isRunning || world is null || economy is null || employmentIndex is null)
                throw new InvalidOperationException("Simulation is not running.");

            var generator = new PopulationGenerator(
                world,
                networkIds,
                economy,
                employmentIndex,
                mapContext,
                relationshipIndex,
                relationshipService,
                relationshipOptions);
            return generator.Generate(totalGenoses, seed, adults, children, tick);
        }
    }

    /// <summary>
    /// Drops queued commands and feedback that belong to the world that is being
    /// replaced. Persistent player profiles and the world itself are left alone;
    /// Initialize resets those.
    /// </summary>
    public void ClearTransientState()
    {
        while (queuedCommands.TryDequeue(out _))
        {
        }

        commandQueue?.Drain(_ => { });

        while (adminCommands.TryDequeue(out var command))
        {
            if (adminWaiters.TryRemove(command.CommandId, out var waiter))
            {
                waiter.TrySetResult(new CommandResult(
                    command.CommandId,
                    false,
                    "The world was reset before the command was applied.",
                    null,
                    DateTimeOffset.UtcNow));
            }
        }

        commandResults.Clear();
        seenAdminCommands.Clear();
        while (intentRejections.TryDequeue(out _))
        {
        }

        while (playerLifecycleNotices.TryDequeue(out _))
        {
        }

        pendingPlayerCommands.Clear();
        playerCommandResults.Clear();
        playerCommandAudit.Clear();
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
            if (commandResults.TryGetValue(command.CommandId, out var existingResult))
            {
                if (adminWaiters.TryRemove(command.CommandId, out var existingWaiter))
                    existingWaiter.TrySetResult(existingResult);
                continue;
            }

            CommandResult result;
            using (WriteLock())
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
            if (adminWaiters.TryRemove(command.CommandId, out var waiter))
                waiter.TrySetResult(result);

            if (commandResults.Count > 1000)
                commandResults.Clear();
        }
    }

    private object? ExecuteAdminCommand(World world, AdminCommand command)
    {
        var tick = this.tick;
        switch (command)
        {
            case CreateGenosCommand createGenos:
            {
                var repository = new GenosRepository(world, networkIds, economy, mapContext);
                var networkId = repository.Add(createGenos.Name, createGenos.Age).Value;
                Emit(new GenosCreated(networkId, createGenos.Name, createGenos.Age));
                return networkId;
            }
            case CreateCompanyCommand createCompany:
            {
                var repository = new CompanyRepository(
                    world,
                    employmentIndex!,
                    networkIds,
                    economy,
                    map: mapContext);
                var networkId = repository.Add(createCompany.Name).Value;
                Emit(new CompanyCreated(networkId, createCompany.Name));
                return networkId;
            }
            case DeleteGenosCommand deleteGenos:
            {
                var repository = new GenosRepository(world, networkIds, economy, mapContext);
                return repository.Delete(deleteGenos.GenosNetworkId);
            }
            case SetGenosActionCommand setAction:
            {
                var repository = new GenosRepository(world, networkIds, economy, mapContext);
                repository.SetAction(setAction.GenosNetworkId, setAction.ActionType);
                return true;
            }
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

            case RelocateBuildingCommand relocate:
            {
                if (mapContext is null)
                    throw new InvalidOperationException("Map is not initialized.");
                if (!networkIds.TryGetEntity(new NetworkId(relocate.BuildingId), out var building) ||
                    !world.IsAlive(building))
                    throw new InvalidOperationException("Building not found.");

                var service = new BuildingRelocationService(world, mapContext, movementIndex);
                var result = service.TryRelocate(
                    relocate.BuildingId, relocate.X, relocate.Y, relocate.EntryX, relocate.EntryY);
                if (!result.Success || result.Plan is null)
                    throw new InvalidOperationException($"{result.Error}: {result.Message}");

                var plan = result.Plan;
                economy?.Emit(new BuildingRelocated(
                    mapContext.Options.MapId,
                    mapContext.Version,
                    relocate.BuildingId,
                    plan.Previous.Anchor.X,
                    plan.Previous.Anchor.Y,
                    plan.Proposed.Anchor.X,
                    plan.Proposed.Anchor.Y,
                    plan.Proposed.Footprint.Width,
                    plan.Proposed.Footprint.Height,
                    plan.Proposed.Entry.X,
                    plan.Proposed.Entry.Y,
                    tick));

                return new
                {
                    buildingId = relocate.BuildingId,
                    relocated = true,
                    placement = new
                    {
                        x = plan.Proposed.Anchor.X,
                        y = plan.Proposed.Anchor.Y,
                        width = plan.Proposed.Footprint.Width,
                        height = plan.Proposed.Footprint.Height,
                        entryX = plan.Proposed.Entry.X,
                        entryY = plan.Proposed.Entry.Y
                    },
                    residents = plan.Residents.Count,
                    employees = plan.Employees.Count,
                    students = plan.Students.Count
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
                if (!progression.ForceStage(force.GenosId, force.Stage, tick))
                    throw new InvalidOperationException("Genos not found.");
                progression.Update(tick, clock);
                return new { genosId = force.GenosId, stage = force.Stage.ToString() };
            }

            case GraduateGenosCommand graduate:
            {
                var progression = new StageProgressionSystem(world, economy!);
                if (!progression.ForceStage(graduate.GenosId, LifeStage.Work, tick))
                    throw new InvalidOperationException("Genos not found.");
                progression.Update(tick, clock);
                return new { genosId = graduate.GenosId, stage = LifeStage.Work.ToString() };
            }

            case RetireGenosCommand retire:
            {
                var progression = new StageProgressionSystem(world, economy!);
                if (!progression.ForceStage(retire.GenosId, LifeStage.Retirement, tick))
                    throw new InvalidOperationException("Genos not found.");
                progression.Update(tick, clock);
                return new { genosId = retire.GenosId, stage = LifeStage.Retirement.ToString() };
            }

            case GrantCareCommand care:
            {
                if (!networkIds.TryGetEntity(new NetworkId(care.GenosId), out var entity) ||
                    !world.IsAlive(entity) || world.Has<Dead>(entity))
                    throw new InvalidOperationException("Genos not found.");

                if (!world.Has<Health>(entity))
                    world.Add(entity, new Health(50f));

                var health = world.Get<Health>(entity);
                var before = health.Value;
                health.Value = Math.Clamp(health.Value + Math.Max(1f, care.HealthGain), 0f, 100f);
                health.LastCareTick = tick;
                world.Get<Health>(entity) = health;

                var cost = Money.FromUnits(25m);
                var shortfall = Money.Zero;
                if (economy.Housing.HouseholdByGenos.TryGetValue(entity, out var household))
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
                        EconomyAccountIds.Government, care.GenosId, shortfall, "medical.care");
                }

                economy.Emit(new MedicalCarePurchased(care.GenosId, cost.Cents, tick));
                economy.Emit(new HealthRestored(care.GenosId, health.Value, tick));
                return new { genosId = care.GenosId, healthBefore = before, healthAfter = health.Value };
            }

            case GeneratePopulationCommand generate:
            {
                return GeneratePopulation(
                    generate.TotalGenoses,
                    generate.Seed,
                    generate.Adults,
                    generate.Children);
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

            case PromoteGenosCommand promote:
            {
                if (!networkIds.TryGetEntity(new NetworkId(promote.GenosId), out var entity) ||
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
                    promote.GenosId,
                    from.ToString(),
                    employee.Seniority.ToString(),
                    employee.SalaryPerCycle.Cents,
                    tick));
                return new
                {
                    genosId = promote.GenosId,
                    seniority = employee.Seniority.ToString(),
                    salary = employee.SalaryPerCycle.Cents
                };
            }

            case SetBenefitEligibilityCommand eligibility:
            {
                if (!networkIds.TryGetEntity(new NetworkId(eligibility.GenosId), out var entity) ||
                    !world.IsAlive(entity))
                    throw new InvalidOperationException("Genos not found.");

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

                return new { genosId = eligibility.GenosId, eligible = eligibility.Eligible };
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
                if (!networkIds.TryGetEntity(new NetworkId(forceDeath.GenosId), out var entity) ||
                    !world.IsAlive(entity))
                    throw new InvalidOperationException("Genos not found.");
                if (world.Has<Dead>(entity))
                    return new { genosId = forceDeath.GenosId, alreadyDead = true };

                world.Add(entity, new Dead(tick, forceDeath.Cause));
                economy.Life.DeathQueue.Add(entity);
                economy.Life.DeathsThisYear++;
                return new { genosId = forceDeath.GenosId, cause = forceDeath.Cause.ToString() };
            }

            case AdvanceAgeCommand advanceAge:
            {
                if (!networkIds.TryGetEntity(new NetworkId(advanceAge.GenosId), out var entity) ||
                    !world.IsAlive(entity) || !world.Has<Alive>(entity))
                    throw new InvalidOperationException("Genos not found.");

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
                return new { genosId = advanceAge.GenosId, age = world.Get<Alive>(entity).Age };
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
                if (!repository.SetEnrollment(enrollment.GenosId, enrollment.SchoolId, enrollment.Enrolled))
                    throw new InvalidOperationException("Enrollment change failed.");
                return new
                {
                    genosId = enrollment.GenosId,
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
                if (!repository.Fire(fire.GenosNetworkId, tick, companyInitiated: true))
                    throw new InvalidOperationException("Employee not found.");
                return new { genosId = fire.GenosNetworkId };
            }

            case ResignEmployeeCommand resign:
            {
                var repository = new EmploymentRepository(world, employmentIndex!, networkIds, economy);
                if (!repository.Resign(resign.GenosNetworkId, tick))
                    throw new InvalidOperationException("Employee not found.");
                return new { genosId = resign.GenosNetworkId };
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
                var id = repository.Create(createHousehold.HeadGenosId, tick);
                return new { householdId = id.Value };
            }

            case AddHouseholdMemberCommand addMember:
            {
                var repository = new HouseholdRepository(world, networkIds, economy!);
                if (!repository.AddMember(
                        new NetworkId(addMember.HouseholdId),
                        addMember.GenosId,
                        addMember.Role,
                        tick))
                    throw new InvalidOperationException("Household not found.");
                return new { householdId = addMember.HouseholdId, genosId = addMember.GenosId };
            }

            case RemoveHouseholdMemberCommand removeMember:
            {
                var repository = new HouseholdRepository(world, networkIds, economy!);
                if (!repository.RemoveMember(
                        new NetworkId(removeMember.HouseholdId),
                        removeMember.GenosId,
                        tick))
                    throw new InvalidOperationException("Household membership not found.");
                return new { householdId = removeMember.HouseholdId, genosId = removeMember.GenosId };
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

            case ForcePartnershipCommand forcePartnership:
            {
                var first = ResolveGenos(world, forcePartnership.GenosAId);
                var second = ResolveGenos(world, forcePartnership.GenosBId);
                if (!forcePartnership.OverrideEligibility)
                {
                    var eligibility = EligibilityPolicy.Evaluate(
                        world, relationshipIndex, first, second, relationshipOptions);
                    if (!eligibility.Eligible)
                        throw new InvalidOperationException(
                            $"Pair is not eligible: {eligibility.Code} — {eligibility.Reason}");
                }

                if (!relationshipService!.TryCreatePair(
                        first, second, RelationshipKind.Partner,
                        RelationshipSource.Admin, tick, out _, out _, out var error))
                    throw new InvalidOperationException($"Cannot create partnership: {error}.");

                economy!.Emit(new PartnershipFormed(
                    forcePartnership.GenosAId, forcePartnership.GenosBId, 0, tick));
                EmitFamilyAdminAction(
                    forcePartnership, "force-partnership", forcePartnership.GenosAId, forcePartnership.GenosBId, 0);
                return new
                {
                    genosAId = forcePartnership.GenosAId,
                    genosBId = forcePartnership.GenosBId,
                    overrideEligibility = forcePartnership.OverrideEligibility
                };
            }

            case EndPartnershipCommand endPartnership:
            {
                var first = ResolveGenos(world, endPartnership.GenosAId);
                var second = ResolveGenos(world, endPartnership.GenosBId);
                if (!relationshipService!.EndPartnership(first, second, tick))
                    throw new InvalidOperationException("No active partnership exists for the pair.");

                economy!.Emit(new PartnershipEnded(
                    endPartnership.GenosAId, endPartnership.GenosBId, endPartnership.Reason, tick));
                EmitFamilyAdminAction(
                    endPartnership, "end-partnership", endPartnership.GenosAId, endPartnership.GenosBId, 0);
                return new { genosAId = endPartnership.GenosAId, genosBId = endPartnership.GenosBId };
            }

            case StartPregnancyCommand startPregnancy:
            {
                var carrying = ResolveGenos(world, startPregnancy.CarryingParentId);
                var other = ResolveGenos(world, startPregnancy.OtherParentId);
                var dueTick = tick + (long)reproductionOptions.PregnancyMonths * clock.TicksPerMonth;
                var health = world.Has<Health>(carrying)
                    ? (int)world.Get<Health>(carrying).Value
                    : 100;
                if (!pregnancyService!.TryStart(
                        carrying, other, tick, dueTick, health,
                        PregnancySource.Admin, reproductionOptions, out var pregnancy, out var error))
                    throw new InvalidOperationException($"Cannot start pregnancy: {error}.");

                var pregnancyId = GetNetworkId(world, pregnancy);
                economy!.Emit(new PregnancyStarted(
                    pregnancyId, startPregnancy.CarryingParentId, startPregnancy.OtherParentId,
                    tick, dueTick, tick));
                EmitFamilyAdminAction(
                    startPregnancy, "start-pregnancy", startPregnancy.CarryingParentId,
                    startPregnancy.OtherParentId, pregnancyId);
                return new { pregnancyId, dueTick };
            }

            case CancelPregnancyCommand cancelPregnancy:
            {
                if (!networkIds.TryGetEntity(new NetworkId(cancelPregnancy.PregnancyId), out var pregnancy) ||
                    !world.IsAlive(pregnancy) || !world.Has<Pregnancy>(pregnancy))
                    throw new InvalidOperationException("Pregnancy not found.");
                if (!pregnancyService!.Cancel(pregnancy, tick))
                    throw new InvalidOperationException("Pregnancy is already resolved.");

                economy!.Emit(new PregnancyCancelled(
                    cancelPregnancy.PregnancyId, cancelPregnancy.Reason, tick));
                EmitFamilyAdminAction(
                    cancelPregnancy, "cancel-pregnancy", cancelPregnancy.PregnancyId, 0, 0);
                return new { pregnancyId = cancelPregnancy.PregnancyId };
            }

            case AssignGuardianCommand assignGuardian:
            {
                var child = ResolveGenos(world, assignGuardian.ChildId);
                var guardian = ResolveGenos(world, assignGuardian.GuardianId);
                if (!guardianshipService!.AssignGuardian(
                        child, guardian, tick, assignGuardian.Reason, out var guardianError))
                    throw new InvalidOperationException($"Cannot assign guardian: {guardianError}.");

                EmitFamilyAdminAction(
                    assignGuardian, "assign-guardian", assignGuardian.ChildId, assignGuardian.GuardianId, 0);
                return new { childId = assignGuardian.ChildId, guardianId = assignGuardian.GuardianId };
            }

            case ReleaseTrustCommand releaseTrust:
            {
                if (!networkIds.TryGetEntity(new NetworkId(releaseTrust.TrustId), out var trust) ||
                    !world.IsAlive(trust) || !world.Has<TrustAccount>(trust))
                    throw new InvalidOperationException("Trust not found.");
                if (!trustService!.ReleaseNow(trust, releaseTrust.Reason, tick))
                    throw new InvalidOperationException("Trust could not be released.");

                EmitFamilyAdminAction(
                    releaseTrust, "release-trust", releaseTrust.TrustId, 0, 0);
                return new { trustId = releaseTrust.TrustId };
            }

            case SetPlayerContinuationCommand setContinuation:
            {
                if (!playerRegistry.TryGet(setContinuation.PlayerId, out var profile))
                    throw new InvalidOperationException("Player not found.");
                if (!Enum.TryParse<PlayerContinuationPolicy>(setContinuation.Policy, ignoreCase: true, out var policy))
                    throw new InvalidOperationException("Unknown continuation policy.");

                profile.ContinuationPolicy = policy;
                profile.Observer = policy == PlayerContinuationPolicy.Observer;
                economy!.Emit(new PlayerAdminActionApplied(
                    setContinuation.CommandId,
                    "set-continuation",
                    setContinuation.IssuedBy,
                    setContinuation.PlayerId,
                    setContinuation.Reason,
                    tick,
                    policy.ToString()));
                return new { playerId = profile.PlayerId, policy = policy.ToString() };
            }

            case DetachPlayerGenosCommand detach:
            {
                if (!playerRegistry.TryGet(detach.PlayerId, out var profile))
                    throw new InvalidOperationException("Player not found.");

                var previousGenosId = profile.ControlledGenos != Entity.Null &&
                                    world.IsAlive(profile.ControlledGenos)
                    ? GetNetworkId(world, profile.ControlledGenos)
                    : profile.LastGenosNetworkId;
                if (profile.ControlledGenos != Entity.Null && world.IsAlive(profile.ControlledGenos) &&
                    world.Has<PlayerControlled>(profile.ControlledGenos))
                {
                    world.Remove<PlayerControlled>(profile.ControlledGenos);
                }
                profile.LastGenosNetworkId = previousGenosId;
                profile.ControlledGenos = Entity.Null;
                profile.AwaitingContinuation = true;
                economy!.Emit(new PlayerAwaitingContinuation(
                    previousGenosId,
                    profile.PlayerId,
                    tick));
                economy.Emit(new PlayerAdminActionApplied(
                    detach.CommandId,
                    "detach",
                    detach.IssuedBy,
                    detach.PlayerId,
                    detach.Reason,
                    tick));
                return new { playerId = profile.PlayerId, detached = true };
            }

            case ForcePlayerContinuationCommand forceContinuation:
            {
                if (!playerRegistry.TryGet(forceContinuation.PlayerId, out var profile))
                    throw new InvalidOperationException("Player not found.");
                if (!Enum.TryParse<PlayerContinuationPolicy>(forceContinuation.Policy, ignoreCase: true, out var policy))
                    throw new InvalidOperationException("Unknown continuation policy.");

                profile.ContinuationPolicy = policy;
                profile.Observer = policy == PlayerContinuationPolicy.Observer;
                if (profile.ControlledGenos != Entity.Null && world.IsAlive(profile.ControlledGenos) &&
                    world.Has<PlayerControlled>(profile.ControlledGenos))
                {
                    world.Remove<PlayerControlled>(profile.ControlledGenos);
                }
                profile.LastGenosNetworkId = profile.ControlledGenos != Entity.Null &&
                                           world.IsAlive(profile.ControlledGenos)
                    ? GetNetworkId(world, profile.ControlledGenos)
                    : profile.LastGenosNetworkId;
                profile.ControlledGenos = Entity.Null;
                profile.AwaitingContinuation = true;
                economy!.Emit(new PlayerAdminActionApplied(
                    forceContinuation.CommandId,
                    "force-continuation",
                    forceContinuation.IssuedBy,
                    forceContinuation.PlayerId,
                    forceContinuation.Reason,
                    tick,
                    policy.ToString()));
                return new { playerId = profile.PlayerId, policy = policy.ToString(), awaiting = true };
            }

            case RestockStoreCommand restock:
            {
                if (!networkIds.TryGetEntity(new NetworkId(restock.StoreNetworkId), out var store) ||
                    !world.Has<Office>(store))
                    throw new InvalidOperationException("Store not found.");

                var grocery = new Genelife.Consumption.GroceryStoreService(
                    world,
                    economy!,
                    entity => networkIds.AllocateInfrastructure(entity));
                var outcome = grocery.RestockStore(store, tick, economy!.Clock.MonthOfYear(tick));
                economy.Emit(new ConsumptionAdminActionApplied(
                    restock.CommandId,
                    "restock",
                    restock.IssuedBy,
                    restock.StoreNetworkId.ToString(),
                    restock.Reason,
                    tick));
                if (outcome.Needed && !outcome.Restocked)
                    throw new InvalidOperationException(
                        "Restock failed: neither the store nor the government can fund the supplier cost.");
                return new
                {
                    store = restock.StoreNetworkId,
                    restocked = outcome.Restocked,
                    units = outcome.Units,
                    costCents = outcome.Cost.Cents
                };
            }

            case RepairUtilityNetworkCommand repair:
            {
                if (!Enum.TryParse<UtilityService>(repair.Service, ignoreCase: true, out var service))
                    throw new InvalidOperationException("Unknown utility service.");

                var utility = new Genelife.Consumption.UtilityNetworkService(
                    world,
                    economy!,
                    entity => networkIds.AllocateInfrastructure(entity),
                    economy!.UtilityOptions);
                var cost = Money.FromUnits(repair.Units * 2m);
                if (!utility.ExpandCapacity(service, repair.Units, cost, tick))
                    throw new InvalidOperationException("Utility network repair failed.");

                economy.Emit(new ConsumptionAdminActionApplied(
                    repair.CommandId,
                    "utility-repair",
                    repair.IssuedBy,
                    repair.Service,
                    repair.Reason,
                    tick));
                return new { service = service.ToString(), units = repair.Units };
            }

            default:
                throw new InvalidOperationException("Unknown admin command.");
        }
    }

    private Entity ResolveGenos(World world, ulong networkId)
    {
        if (!networkIds.TryGetEntity(new NetworkId(networkId), out var entity) ||
            !world.IsAlive(entity) || !world.Has<Alive>(entity) || world.Has<Dead>(entity))
            throw new InvalidOperationException($"Genos {networkId} not found.");
        return entity;
    }

    private void EmitFamilyAdminAction(
        AdminCommand command,
        string action,
        ulong subjectA,
        ulong subjectB,
        ulong subjectC)
    {
        var reason = command switch
        {
            ForcePartnershipCommand force => force.Reason,
            EndPartnershipCommand end => end.Reason,
            StartPregnancyCommand start => start.Reason,
            CancelPregnancyCommand cancel => cancel.Reason,
            AssignGuardianCommand assign => assign.Reason,
            ReleaseTrustCommand release => release.Reason,
            _ => string.Empty
        };

        economy?.Emit(new FamilyAdminActionApplied(
            command.CommandId,
            action,
            command.IssuedBy,
            reason,
            subjectA,
            subjectB,
            subjectC,
            tick));
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
        using (WriteLock())
        {
            if (mapContext is null)
                return new MapStatus();

            var map = mapContext.Map;
            var options = map.Options;
            var result = new MapStatus
            {
                HasMap = true,
                MapId = options.MapId,
                Version = map.Version,
                LayoutVersion = options.Version,
                Seed = options.Seed,
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

        using (WriteLock())
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

        using (WriteLock())
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
        using (WriteLock())
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
        using (WriteLock())
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
        using (WriteLock())
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
        List<GenosStatus>? genosesFallback = null;
        List<CompanyStatus>? companiesFallback = null;
        var offices = new List<OfficeRecord>();
        var employees = new List<EmployeeRecord>();
        var postings = new List<PostingRecord>();
        var openSeatPostings = new List<Entity>();
        var applications = new List<ApplicationRecord>();
        var employeeCountsByCompany = new Dictionary<ulong, int>();
        long currentTick;
        bool running;

        using (WriteLock())
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
                genosesFallback = BuildGenosSnapshots(currentWorld);
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

        var genoses = snapshot?.Genoses ?? genosesFallback ?? new List<GenosStatus>();
        var companies = snapshot?.Companies ?? companiesFallback ?? new List<CompanyStatus>();

        var result = new WorldDataStatus
        {
            Tick = currentTick,
            Year = clock.YearOf(currentTick),
            Month = clock.MonthOfYear(currentTick),
            IsRunning = running,
            CapturedAt = DateTimeOffset.UtcNow,
            Genoses = genoses,
            Map = BuildMapSummary()
        };

        var genosNames = new Dictionary<ulong, string>();
        foreach (var genos in genoses)
            genosNames[genos.NetworkId] = genos.Name;

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
            genosNames.TryGetValue(employee.GenosNetworkId, out var genosName);
            companyNames.TryGetValue(employee.CompanyNetworkId, out var companyName);

            result.Employees.Add(new EmployeeStatus
            {
                GenosNetworkId = employee.GenosNetworkId,
                Genos = genosName ?? string.Empty,
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
            genosNames.TryGetValue(application.ApplicantNetworkId, out var applicantName);

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
        using (WriteLock())
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
        using (WriteLock())
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
        using (WriteLock())
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
            status.TotalGenoses = total;
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
    /// scanning every genos and pension account.
    /// </summary>
    public LifecycleStatus BuildLifecycleSummary()
    {
        using (WriteLock())
        {
            var status = new LifecycleStatus();
            if (world is null || economy is null)
                return status;

            var index = economy.Life.Index;
            var latest = economy.Life.MetricHistory.Count > 0 ? economy.Life.MetricHistory[^1] : null;

            status.TotalGenoses = index.TotalLiving;
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

            var traitIssues = TraitInvariants.Verify(world);
            if (traitIssues.Count == 0)
            {
                status.Health.Add(new LifecycleHealthCheckStatus
                {
                    Name = "traits",
                    Level = "Pass",
                    Detail = "All living genoses have valid trait sets"
                });
            }
            else
            {
                status.Health.AddRange(traitIssues.Select(issue => new LifecycleHealthCheckStatus
                {
                    Name = issue.Name,
                    Level = issue.Level,
                    Detail = issue.Detail
                }));
            }

            var relationshipIssues = RelationshipInvariants.Verify(world, relationshipIndex);
            if (relationshipIssues.Count == 0)
            {
                status.Health.Add(new LifecycleHealthCheckStatus
                {
                    Name = "relationships",
                    Level = "Pass",
                    Detail = "Relationship edges and mirrors are consistent"
                });
            }
            else
            {
                status.Health.AddRange(relationshipIssues.Select(issue => new LifecycleHealthCheckStatus
                {
                    Name = issue.Name,
                    Level = issue.Level,
                    Detail = issue.Detail
                }));
            }

            status.Health.AddRange(ReproductionInvariants.Verify(world, relationshipIndex, economy)
                .Select(issue => new LifecycleHealthCheckStatus
                {
                    Name = issue.Name,
                    Level = issue.Level,
                    Detail = issue.Detail
                }));
            status.Health.AddRange(ContinuityInvariants.Verify(world, relationshipIndex, economy)
                .Select(issue => new LifecycleHealthCheckStatus
                {
                    Name = issue.Name,
                    Level = issue.Level,
                    Detail = issue.Detail
                }));

            return status;
        }
    }

    public ConsumptionOverviewStatus BuildConsumptionOverview()
    {
        using (WriteLock())
        {
            if (world is null || economy is null)
                return new ConsumptionOverviewStatus(
                    Array.Empty<UtilityNetworkStatus>(), 0, 0, 0, 0, 0, 0,
                    Array.Empty<NutritionBucketStatus>(), 0, GroceryCatalog.Version);

            var networks = new List<UtilityNetworkStatus>();
            world.Query(in new QueryDescription().WithAll<UtilityNetwork>(),
                (Entity entity, ref UtilityNetwork _) =>
                    networks.Add(ConsumptionReadModel.Network(world, entity)));
            networks.Sort((a, b) => string.Compare(a.Service, b.Service, StringComparison.Ordinal));

            var households = 0;
            var cutoff = 0;
            var reduced = 0;
            world.Query(in new QueryDescription().WithAll<Household, HouseholdMeter>(),
                (ref Household _, ref HouseholdMeter meter) =>
                {
                    households++;
                    if (meter.WaterLevel == ServiceLevel.CutOff || meter.ElectricityLevel == ServiceLevel.CutOff) cutoff++;
                    else if (meter.WaterLevel == ServiceLevel.Reduced || meter.ElectricityLevel == ServiceLevel.Reduced) reduced++;
                });

            var stores = new HashSet<Entity>();
            var stockUnits = 0;
            world.Query(in new QueryDescription().WithAll<StoreInventory, StoreStockLink>(),
                (ref StoreInventory inventory, ref StoreStockLink link) =>
                {
                    stores.Add(link.Store);
                    stockUnits += Math.Max(0, inventory.Stock);
                });

            var healthy = 0;
            var poor = 0;
            var critical = 0;
            world.Query(in new QueryDescription().WithAll<NutritionState>(),
                (ref NutritionState nutrition) =>
                {
                    if (nutrition.Balance > 20) healthy++;
                    else if (nutrition.Balance < 0) critical++;
                    else poor++;
                });

            var buckets = new List<NutritionBucketStatus>
            {
                new("balanced", healthy),
                new("marginal", poor),
                new("poor", critical)
            };

            return new ConsumptionOverviewStatus(
                networks,
                households,
                cutoff,
                reduced,
                stores.Count,
                stockUnits,
                0,
                buckets,
                critical,
                GroceryCatalog.Version);
        }
    }

    public IReadOnlyList<UtilityNetworkStatus> BuildConsumptionNetworks()
    {
        using (WriteLock())
        {
            if (world is null)
                return Array.Empty<UtilityNetworkStatus>();
            var rows = new List<UtilityNetworkStatus>();
            world.Query(in new QueryDescription().WithAll<UtilityNetwork>(),
                (Entity entity, ref UtilityNetwork _) => rows.Add(ConsumptionReadModel.Network(world, entity)));
            rows.Sort((a, b) => string.Compare(a.Service, b.Service, StringComparison.Ordinal));
            return rows;
        }
    }

    public ConsumptionHouseholdPage BuildConsumptionHouseholds(
        int take = 100,
        int skip = 0,
        string? level = null)
    {
        using (WriteLock())
        {
            if (world is null || economy is null)
                return new ConsumptionHouseholdPage(Array.Empty<ConsumptionHouseholdStatus>(), 0);

            take = Math.Clamp(take, 1, 500);
            skip = Math.Max(0, skip);
            var rows = new List<ConsumptionHouseholdStatus>();
            world.Query(in new QueryDescription().WithAll<Household>(),
                (Entity entity, ref Household household) =>
                {
                    var meter = world.Has<HouseholdMeter>(entity) ? world.Get<HouseholdMeter>(entity) : default;
                    if (!string.IsNullOrWhiteSpace(level) &&
                        !string.Equals(level, "all", StringComparison.OrdinalIgnoreCase))
                    {
                        var matches = level.Trim().ToLowerInvariant() switch
                        {
                            "cutoff" => meter.WaterLevel == ServiceLevel.CutOff || meter.ElectricityLevel == ServiceLevel.CutOff,
                            "reduced" => meter.WaterLevel == ServiceLevel.Reduced || meter.ElectricityLevel == ServiceLevel.Reduced,
                            "full" => meter.WaterLevel == ServiceLevel.Full && meter.ElectricityLevel == ServiceLevel.Full,
                            "arrears" => meter.Arrears.IsPositive || household.RentArrears.IsPositive,
                            _ => true
                        };
                        if (!matches) return;
                    }
                    rows.Add(ConsumptionReadModel.Household(
                        world,
                        entity,
                        household.Unit,
                        economy.Housing.MemberCount(entity)));
                });
            rows.Sort((a, b) => b.ArrearsCents.CompareTo(a.ArrearsCents));
            return new ConsumptionHouseholdPage(rows.Skip(skip).Take(take).ToList(), rows.Count);
        }
    }

    public ConsumptionStorePage BuildConsumptionStores(int take = 100, int skip = 0)
    {
        using (WriteLock())
        {
            if (world is null)
                return new ConsumptionStorePage(Array.Empty<ConsumptionStoreStatus>(), 0);

            var stores = new Dictionary<Entity, List<StoreItemStatus>>();
            world.Query(in new QueryDescription().WithAll<StoreInventory, StoreStockLink>(),
                (ref StoreInventory inventory, ref StoreStockLink link) =>
                {
                    if (!stores.TryGetValue(link.Store, out var items))
                    {
                        items = new List<StoreItemStatus>();
                        stores[link.Store] = items;
                    }
                    var item = inventory.ItemIndex >= 0 && inventory.ItemIndex < GroceryCatalog.Items.Count
                        ? GroceryCatalog.Items[inventory.ItemIndex].Id
                        : $"item-{inventory.ItemIndex}";
                    items.Add(new StoreItemStatus(
                        inventory.ItemIndex,
                        item,
                        inventory.Stock,
                        inventory.CurrentPrice.Cents,
                        inventory.LastRestockTick));
                });

            var rows = stores
                .OrderBy(pair => GetNetworkId(world, pair.Key))
                .Select(pair => new ConsumptionStoreStatus(
                    GetNetworkId(world, pair.Key),
                    world.Has<Office>(pair.Key) ? world.Get<Office>(pair.Key).OfficeName : $"Store {GetNetworkId(world, pair.Key)}",
                    pair.Value.OrderBy(item => item.ItemIndex).ToList()))
                .ToList();

            return new ConsumptionStorePage(rows.Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 500)).ToList(), rows.Count);
        }
    }

    public PlayerPresenceDto? BuildPlayerPresence(string playerId)
    {
        using (WriteLock())
        {
            if (world is null || string.IsNullOrWhiteSpace(playerId) ||
                !playerRegistry.TryGet(playerId, out var profile) ||
                profile.ControlledGenos == Entity.Null ||
                !world.IsAlive(profile.ControlledGenos) ||
                world.Has<Dead>(profile.ControlledGenos))
                return null;

            var genos = profile.ControlledGenos;
            var tile = world.Has<TilePosition>(genos) ? world.Get<TilePosition>(genos).Tile : default;
            Entity building = Entity.Null;
            world.Query(in new QueryDescription().WithAll<Placement>(),
                (Entity entity, ref Placement placement) =>
                {
                    if (building == Entity.Null
                        && (placement.Covers(tile) || placement.Entry == tile))
                        building = entity;
                });

            var buildingId = building != Entity.Null ? GetNetworkId(world, building) : (ulong?)null;
            var inVenue = building != Entity.Null
                && world.Has<SocialVenue>(building)
                && venueService?.IsOccupant(playerId, building) == true;
            var appearance = AppearanceMapper.TryDerive(world, genos, out var descriptor)
                ? AppearanceMapper.ToDto(descriptor)
                : null;

            return new PlayerPresenceDto(
                playerId,
                world.Has<GenName>(genos) ? world.Get<GenName>(genos).Name : null,
                GetNetworkId(world, genos),
                tile.X,
                tile.Y,
                buildingId,
                inVenue ? PresenceState.InVenue.ToString() : PresenceState.Online.ToString(),
                appearance,
                tick);
        }
    }

    public IReadOnlyList<PlayerPresenceDto> BuildPlayerPresence(IReadOnlyCollection<string> playerIds)
    {
        var rows = new List<PlayerPresenceDto>();
        foreach (var playerId in playerIds.OrderBy(id => id, StringComparer.Ordinal))
        {
            var presence = BuildPlayerPresence(playerId);
            if (presence is not null)
                rows.Add(presence);
        }
        return rows;
    }

    public AppearancePage BuildAppearancePage(int take = 100, int skip = 0, string? search = null)
    {
        using (WriteLock())
        {
            if (world is null)
                return new AppearancePage(Array.Empty<AppearanceGenosStatus>(), 0);

            take = Math.Clamp(take, 1, 500);
            skip = Math.Max(0, skip);
            var rows = new List<AppearanceGenosStatus>();
            var query = new QueryDescription().WithAll<GenName, Traits, Alive>().WithNone<Dead>();

            world.Query(in query, (Entity entity, ref GenName name, ref Traits traits, ref Alive alive) =>
            {
                if (!string.IsNullOrWhiteSpace(search) &&
                    !name.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    !AppearanceMapper.StableKey(world, entity).ToString().Contains(search.Trim(), StringComparison.Ordinal))
                    return;
                if (!AppearanceMapper.TryDerive(world, entity, out var descriptor))
                    return;
                rows.Add(Genelife.Api.Appearance.AppearanceReadModel.Build(world, entity));
            });

            rows.Sort((a, b) =>
            {
                var byName = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                return byName != 0 ? byName : a.NetworkId.CompareTo(b.NetworkId);
            });

            return new AppearancePage(rows.Skip(skip).Take(take).ToList(), rows.Count);
        }
    }

    public AppearanceGenosStatus? BuildAppearance(ulong genosNetworkId)
    {
        using (WriteLock())
        {
            if (world is null || !networkIds.TryGetEntity(new NetworkId(genosNetworkId), out var entity))
                return null;
            if (!AppearanceMapper.TryDerive(world, entity, out _))
                return null;
            return Genelife.Api.Appearance.AppearanceReadModel.Build(world, entity);
        }
    }

    public AppearanceFamilyStatus? BuildAppearanceFamily(ulong genosNetworkId)
    {
        using (WriteLock())
        {
            if (world is null || !networkIds.TryGetEntity(new NetworkId(genosNetworkId), out var focal))
                return null;
            if (!AppearanceMapper.TryDerive(world, focal, out var focalDescriptor))
                return null;

            var members = new List<AppearanceFamilyMemberStatus>();
            var related = new HashSet<Entity>();
            var parents = relationshipIndex.ParentsOf(focal);
            var children = relationshipIndex.ChildrenOf(focal);

            foreach (var parent in parents)
                AddMember(members, related, parent, "parent", focalDescriptor);
            foreach (var child in children)
                AddMember(members, related, child, "child", focalDescriptor);

            var siblings = new HashSet<Entity>();
            foreach (var parent in parents)
                foreach (var sibling in relationshipIndex.ChildrenOf(parent))
                    if (sibling != focal)
                        siblings.Add(sibling);
            foreach (var sibling in siblings)
                AddMember(members, related, sibling, "sibling", focalDescriptor);

            return new AppearanceFamilyStatus(
                Genelife.Api.Appearance.AppearanceReadModel.Build(world, focal),
                members);
        }
    }

    private void AddMember(
        List<AppearanceFamilyMemberStatus> members,
        HashSet<Entity> seen,
        Entity entity,
        string relation,
        AppearanceDescriptor focal)
    {
        if (entity == Entity.Null || !world!.IsAlive(entity) || world.Has<Dead>(entity) ||
            !seen.Add(entity) || !AppearanceMapper.TryDerive(world, entity, out var descriptor))
            return;

        var resemblance = FamilyResemblance.Compare(focal, descriptor);
        members.Add(new AppearanceFamilyMemberStatus(
            relation,
            Genelife.Api.Appearance.AppearanceReadModel.Build(world, entity),
            resemblance.SharesSkinTone,
            resemblance.SharesFaceShape,
            resemblance.SharesHairColor,
            resemblance.SharesEyeColor,
            resemblance.SharedFields,
            resemblance.Score));
    }

    public AppearanceGenosStatus? BuildOwnAppearance(string playerId)
    {
        using (WriteLock())
        {
            if (world is null || string.IsNullOrWhiteSpace(playerId) ||
                !playerRegistry.TryGet(playerId, out var profile) ||
                profile.ControlledGenos == Entity.Null || !world.IsAlive(profile.ControlledGenos))
                return null;
            if (!AppearanceMapper.TryDerive(world, profile.ControlledGenos, out _))
                return null;
            return Genelife.Api.Appearance.AppearanceReadModel.Build(world, profile.ControlledGenos);
        }
    }

    public PlayerOverviewStatus BuildPlayerOverview()
    {
        using (WriteLock())
        {
            if (world is null || economy is null)
                return new PlayerOverviewStatus(0, 0, 0, 0, 0, 0, 0, 0, 0);

            var active = 0;
            var awaiting = 0;
            var employed = 0;
            var housed = 0;

            foreach (var profile in playerRegistry.All())
            {
                if (profile.AwaitingContinuation)
                {
                    awaiting++;
                    continue;
                }

                var genos = profile.ControlledGenos;
                if (genos == Entity.Null || !world.IsAlive(genos) || world.Has<Dead>(genos))
                    continue;

                active++;
                if (world.Has<Employee>(genos))
                    employed++;
                if (world.Has<PlayerHousing>(genos) && world.Get<PlayerHousing>(genos).LeaseActive)
                    housed++;
            }

            return new PlayerOverviewStatus(
                playerRegistry.Players.Count,
                active,
                awaiting,
                employed,
                housed,
                CountPendingPlayerInvitations(),
                playerRegistry.DeathsThisYear,
                playerRegistry.ContinuationsThisYear,
                playerRegistry.CommandsAppliedThisYear);
        }
    }

    public PlayerLifePage BuildPlayerLives(
        int take = 100,
        int skip = 0,
        string? search = null,
        string? state = null)
    {
        using (WriteLock())
        {
            if (world is null || economy is null)
                return new PlayerLifePage(Array.Empty<PlayerLifeStatus>(), 0);

            take = Math.Clamp(take, 1, 500);
            skip = Math.Max(0, skip);

            var rows = new List<PlayerLifeStatus>();
            foreach (var profile in playerRegistry.All().OrderBy(p => p.PlayerId, StringComparer.Ordinal))
            {
                var status = BuildPlayerLifeStatus(profile);
                if (!MatchesPlayerFilter(status, search, state))
                    continue;
                rows.Add(status);
            }

            return new PlayerLifePage(rows.Skip(skip).Take(take).ToList(), rows.Count);
        }
    }

    public PlayerLifeStatus? BuildPlayerLife(string playerId)
    {
        using (WriteLock())
        {
            if (world is null || economy is null || string.IsNullOrWhiteSpace(playerId))
                return null;
            if (!playerRegistry.TryGet(playerId, out var profile))
                return null;
            return BuildPlayerLifeStatus(profile);
        }
    }

    public IReadOnlyList<PlayerInvitationStatus> BuildPlayerInvitations(string playerId)
    {
        using (WriteLock())
        {
            if (world is null || string.IsNullOrWhiteSpace(playerId) ||
                !playerRegistry.TryGet(playerId, out var profile) ||
                profile.ControlledGenos == Entity.Null ||
                !world.IsAlive(profile.ControlledGenos))
                return Array.Empty<PlayerInvitationStatus>();

            return PendingInvitations(profile.ControlledGenos).ToList();
        }
    }

    private PlayerLifeStatus BuildPlayerLifeStatus(PlayerProfile profile)
    {
        var worldRef = world!;
        var genos = profile.ControlledGenos;
        var genosAlive = genos != Entity.Null && worldRef.IsAlive(genos);
        var genosNetworkId = genosAlive && worldRef.Has<NetworkId>(genos)
            ? worldRef.Get<NetworkId>(genos).Value
            : profile.LastGenosNetworkId;

        var name = genosAlive && worldRef.Has<GenName>(genos) ? worldRef.Get<GenName>(genos).Name : null;
        var alive = genosAlive && worldRef.Has<Alive>(genos) ? worldRef.Get<Alive>(genos) : new Alive(0);
        var health = genosAlive && worldRef.Has<Health>(genos) ? worldRef.Get<Health>(genos).Value : 0f;
        var needs = genosAlive && worldRef.Has<Needs>(genos) ? worldRef.Get<Needs>(genos) : new Needs();
        var wallet = genosAlive && worldRef.Has<Wallet>(genos) ? worldRef.Get<Wallet>(genos).Balance.Cents : 0;
        var savings = genosAlive && worldRef.Has<SavingsAccount>(genos)
            ? worldRef.Get<SavingsAccount>(genos).Balance.Cents
            : 0;

        var employment = new PlayerEmploymentStatus(false, 0, 0, string.Empty, string.Empty, 0, false, 0, 0);
        if (genosAlive && worldRef.Has<Employee>(genos))
        {
            var employee = worldRef.Get<Employee>(genos);
            var record = worldRef.Has<EmploymentRecord>(genos) ? worldRef.Get<EmploymentRecord>(genos) : default;
            var playerEmployment = worldRef.Has<PlayerEmployment>(genos)
                ? worldRef.Get<PlayerEmployment>(genos)
                : default;
            employment = new PlayerEmploymentStatus(
                true,
                GetNetworkId(worldRef, employee.Company),
                GetNetworkId(worldRef, employee.Office),
                employee.Position,
                employee.Seniority.ToString(),
                employee.SalaryPerCycle.Cents,
                worldRef.Has<CurrentAction>(genos) && worldRef.Get<CurrentAction>(genos).Type == ActionType.Working,
                playerEmployment.ConsecutiveMissedShifts,
                playerEmployment.LastAttendedTick);
        }

        var housing = new PlayerHousingStatus(false, 0, 0, 0, null, null, null, 0, 0, 0, false, 0, 0);
        if (genosAlive && worldRef.Has<PlayerHousing>(genos))
        {
            var lease = worldRef.Get<PlayerHousing>(genos);
            var household = lease.Household;
            var unit = lease.Unit;
            if (household != Entity.Null && worldRef.IsAlive(household) && worldRef.Has<Household>(household))
            {
                var householdData = worldRef.Get<Household>(household);
                if (unit != Entity.Null && worldRef.IsAlive(unit) && worldRef.Has<HousingUnit>(unit))
                {
                    var unitData = worldRef.Get<HousingUnit>(unit);
                    var property = unitData.Property;
                    var propertyName = property != Entity.Null && worldRef.IsAlive(property) &&
                                       worldRef.Has<Property>(property)
                        ? worldRef.Get<Property>(property).Name
                        : null;
                    var pantry = worldRef.Has<HouseholdPantry>(household)
                        ? worldRef.Get<HouseholdPantry>(household)
                        : default;
                    housing = new PlayerHousingStatus(
                        true,
                        GetNetworkId(worldRef, household),
                        GetNetworkId(worldRef, unit),
                        GetNetworkId(worldRef, property),
                        propertyName,
                        unitData.UnitNumber,
                        unitData.Tier.ToString(),
                        householdData.MonthlyRent.Cents,
                        householdData.RentArrears.Cents,
                        householdData.UtilityArrears.Cents,
                        lease.LeaseActive,
                        pantry.FoodUnits,
                        pantry.HouseholdGoods);
                }
                else
                {
                    housing = housing with
                    {
                        HouseholdId = GetNetworkId(worldRef, household),
                        RentCents = householdData.MonthlyRent.Cents,
                        RentArrearsCents = householdData.RentArrears.Cents,
                        UtilityArrearsCents = householdData.UtilityArrears.Cents
                    };
                }
            }
        }

        PlayerJobOfferStatus? jobOffer = null;
        if (genosAlive && worldRef.Has<PlayerJobOffer>(genos))
        {
            var offer = worldRef.Get<PlayerJobOffer>(genos);
            jobOffer = new PlayerJobOfferStatus(
                offer.OfferId,
                GetNetworkId(worldRef, offer.Posting),
                offer.Position,
                offer.SalaryPerCycle.Cents,
                offer.ExpiresTick);
        }

        PlayerLeaseOfferStatus? leaseOffer = null;
        if (genosAlive && worldRef.Has<PlayerLeaseOffer>(genos))
        {
            var offer = worldRef.Get<PlayerLeaseOffer>(genos);
            leaseOffer = new PlayerLeaseOfferStatus(
                offer.OfferId,
                GetNetworkId(worldRef, offer.Unit),
                GetNetworkId(worldRef, offer.Property),
                offer.MonthlyRent.Cents,
                offer.ExpiresTick);
        }

        ulong? partnerGenosId = null;
        string? partnerName = null;
        var children = 0;
        if (genosAlive)
        {
            var partner = relationshipIndex.PartnersOf(genos)
                .FirstOrNull(candidate => worldRef.IsAlive(candidate) && !worldRef.Has<Dead>(candidate));
            if (partner != Entity.Null && worldRef.IsAlive(partner))
            {
                partnerGenosId = GetNetworkId(worldRef, partner);
                partnerName = worldRef.Has<GenName>(partner) ? worldRef.Get<GenName>(partner).Name : null;
            }
            children = relationshipIndex.ChildrenOf(genos)
                .Count(child => worldRef.IsAlive(child) && !worldRef.Has<Dead>(child));
        }

        var invitations = genosAlive
            ? PendingInvitations(genos).ToList()
            : new List<PlayerInvitationStatus>();

        var audit = GetPlayerCommandAudit(profile.PlayerId, 10)
            .Select(result => new PlayerActionAuditStatus(
                result.CommandId,
                result.Kind.ToString(),
                result.Status.ToString(),
                result.Code,
                result.AppliedTick))
            .ToList();

        return new PlayerLifeStatus(
            profile.PlayerId,
            genosNetworkId,
            name,
            alive.Age,
            health,
            needs.Hunger,
            needs.Energy,
            needs.Hygiene,
            needs.Bladder,
            wallet,
            savings,
            genosAlive && !worldRef.Has<Dead>(genos),
            profile.AwaitingContinuation,
            profile.ContinuationPolicy.ToString(),
            profile.Observer,
            genosAlive && worldRef.Has<NutritionState>(genos) ? worldRef.Get<NutritionState>(genos).Balance : 0,
            genosAlive && worldRef.Has<NutritionState>(genos) ? worldRef.Get<NutritionState>(genos).ConsecutivePoorDays : 0,
            partnerGenosId,
            partnerName,
            children,
            employment,
            housing,
            jobOffer,
            leaseOffer,
            invitations,
            audit);
    }

    private IEnumerable<PlayerInvitationStatus> PendingInvitations(Entity genos)
    {
        var rows = new List<PlayerInvitationStatus>();
        world!.Query(in new QueryDescription().WithAll<FamilyInvitation>(),
            (ref FamilyInvitation invitation) =>
            {
                if (invitation.Status != FamilyInvitationStatus.Pending)
                    return;
                if (invitation.From != genos && invitation.To != genos)
                    return;
                rows.Add(new PlayerInvitationStatus(
                    invitation.Id,
                    GetNetworkId(world!, invitation.From),
                    GetNetworkId(world!, invitation.To),
                    invitation.Kind.ToString(),
                    invitation.Status.ToString(),
                    invitation.ExpiresTick,
                    invitation.CreatedTick));
            });
        return rows
            .OrderByDescending(row => row.CreatedTick)
            .ThenBy(row => row.Id);
    }

    private int CountPendingPlayerInvitations()
    {
        var players = new HashSet<Entity>();
        foreach (var profile in playerRegistry.All())
            if (profile.ControlledGenos != Entity.Null && world!.IsAlive(profile.ControlledGenos))
                players.Add(profile.ControlledGenos);

        var count = 0;
        world!.Query(in new QueryDescription().WithAll<FamilyInvitation>(),
            (ref FamilyInvitation invitation) =>
            {
                if (invitation.Status == FamilyInvitationStatus.Pending &&
                    (players.Contains(invitation.From) || players.Contains(invitation.To)))
                    count++;
            });
        return count;
    }

    private static bool MatchesPlayerFilter(PlayerLifeStatus status, string? search, string? state)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var matches = status.PlayerId.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                          (status.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
            if (!matches)
                return false;
        }

        if (!string.IsNullOrWhiteSpace(state))
        {
            return state.Trim().ToLowerInvariant() switch
            {
                "active" => status.IsAlive && !status.AwaitingContinuation,
                "awaiting" => status.AwaitingContinuation,
                "employed" => status.Employment.Employed,
                "unemployed" => status.IsAlive && !status.Employment.Employed,
                "housed" => status.Housing.Housed,
                "homeless" => status.IsAlive && !status.Housing.Housed,
                _ => true
            };
        }

        return true;
    }

    public FamilyStatus BuildFamilyStatus()
    {
        using (WriteLock())
        {
            var status = new FamilyStatus
            {
                SeedPartnershipShare = relationshipOptions.SeedPartnershipShare,
                SameSexPairingAllowed = relationshipOptions.AllowSameSexPairing,
                CohabitOnPairing = relationshipOptions.CohabitOnPairing
            };
            if (world is null || economy is null)
                return status;

            var adults = 0;
            var minors = 0;
            world.Query(in new QueryDescription().WithAll<Alive>().WithNone<Dead>(),
                (Entity entity, ref Alive alive) =>
                {
                    if (alive.Age < 18)
                        minors++;
                    else
                        adults++;
                });

            status.Population = adults + minors;
            status.Adults = adults;
            status.Minors = minors;
            status.BirthsThisYear = economy.Life.BirthsThisYear;
            status.BirthRate = adults + minors > 0
                ? status.BirthsThisYear * 1_000m / (adults + minors)
                : 0m;
            status.NaturalChange = economy.Life.BirthsThisYear - economy.Life.DeathsThisYear;
            status.InheritancePaidThisYear = economy.Life.InheritancePaidThisYear.Units;
            status.TrustReleasedThisYear = economy.Life.TrustReleasedThisYear.Units;
            status.MinorTrustTotal = trustService?.TotalHeld().Units ?? 0m;
            var latestPopulation = economy.Life.MetricHistory.Count > 0
                ? economy.Life.MetricHistory[^1]
                : null;
            status.Orphans = latestPopulation?.Orphans ?? 0;
            status.StateCareChildren = latestPopulation?.StateCareChildren ?? 0;
            status.ReplacementRate = latestPopulation?.ReplacementRate ?? 0m;
            status.PairedGenoses = relationshipsCount(genos => relationshipIndex.PartnersOf(genos).Count > 0);
            status.SingleAdults = Math.Max(0, adults - status.PairedGenoses);
            status.PartnershipRate = adults > 0 ? status.PairedGenoses / (decimal)adults : 0m;
            status.Traits = BuildTraitDistribution();
            status.Households = BuildHouseholdCompositions();
            status.Projection = new ProjectionStalenessStatus
            {
                Source = "live",
                LiveTick = tick,
                ProjectedTick = null,
                IsStale = false,
                Detail = "Family read model reads live simulation state; Marten projections are history."
            };
            status.Performance = BuildFamilyPerformanceStatus();

            world.Query(in new QueryDescription().WithAll<Relationship>(),
                (Entity _, ref Relationship relationship) =>
                {
                    switch (relationship.Kind)
                    {
                        case RelationshipKind.Parent:
                            status.ParentEdges++;
                            break;
                        case RelationshipKind.Guardian:
                            status.GuardianEdges++;
                            break;
                        case RelationshipKind.Partner:
                            status.Partnerships++;
                            break;
                        case RelationshipKind.ExPartner:
                            status.ExPartnerships++;
                            break;
                    }
                });
            status.Partnerships /= 2;
            status.ExPartnerships /= 2;

            var issues = new List<FamilyHealthCheckStatus>();
            var traitIssues = TraitInvariants.Verify(world);
            issues.AddRange(traitIssues.Select(issue => new FamilyHealthCheckStatus
            {
                Name = issue.Name,
                Level = issue.Level,
                Detail = issue.Detail
            }));

            var relationshipIssues = RelationshipInvariants.Verify(world, relationshipIndex);
            if (relationshipIssues.Count == 0)
            {
                issues.Add(new FamilyHealthCheckStatus
                {
                    Name = "relationships",
                    Level = "Pass",
                    Detail = "Relationship edges and mirrors are consistent"
                });
            }
            else
            {
                issues.AddRange(relationshipIssues.Select(issue => new FamilyHealthCheckStatus
                {
                    Name = issue.Name,
                    Level = issue.Level,
                    Detail = issue.Detail
                }));
            }

            var reproductionIssues = ReproductionInvariants.Verify(world, relationshipIndex, economy);
            issues.AddRange(reproductionIssues.Select(issue => new FamilyHealthCheckStatus
            {
                Name = issue.Name,
                Level = issue.Level,
                Detail = issue.Detail
            }));

            var continuityIssues = ContinuityInvariants.Verify(world, relationshipIndex, economy);
            issues.AddRange(continuityIssues.Select(issue => new FamilyHealthCheckStatus
            {
                Name = issue.Name,
                Level = issue.Level,
                Detail = issue.Detail
            }));

            if (traitIssues.Count == 0)
            {
                issues.Add(new FamilyHealthCheckStatus
                {
                    Name = "traits",
                    Level = "Pass",
                    Detail = "Trait invariants pass"
                });
            }

            var stateCare = 0;
            world.Query(in new QueryDescription().WithAll<StateCare>(),
                (Entity entity, ref StateCare care) =>
                {
                    stateCare++;
                    if (world.Has<Alive>(entity) && world.Get<Alive>(entity).Age < economy.Life.Policy.WorkStartAge &&
                        relationshipIndex.GuardiansOf(entity).Count == 0 &&
                        relationshipIndex.ParentsOf(entity).Any(parent => world.IsAlive(parent) && !world.Has<Dead>(parent)))
                    {
                        issues.Add(new FamilyHealthCheckStatus
                        {
                            Name = "state-care-with-parent",
                            Level = "Warn",
                            Detail = "A child with a living parent is still in state care.",
                            EntityNetworkId = GetNetworkId(world, entity),
                            EntityKind = "genos",
                            Link = $"/family?genos={GetNetworkId(world, entity)}"
                        });
                    }
                });

            issues.Add(new FamilyHealthCheckStatus
            {
                Name = "state-care",
                Level = "Pass",
                Detail = $"{stateCare} minor(s) in state care"
            });

            var candidateCount = relationshipCache.Candidates.Values.Sum(candidates => candidates.Count);
            var candidateCap = relationshipOptions.MaxCandidatesPerGenos;
            var eligibleSingles = relationshipCache.Candidates.Count;
            issues.Add(new FamilyHealthCheckStatus
            {
                Name = "candidate-budget",
                Level = eligibleSingles > 0 && candidateCount > eligibleSingles * candidateCap ? "Fail" : "Pass",
                Detail = $"{candidateCount} candidate(s) across {eligibleSingles} eligible genos(s); cap {candidateCap} per genos."
            });

            issues.Add(new FamilyHealthCheckStatus
            {
                Name = "projection",
                Level = status.Projection.IsStale ? "Warn" : "Pass",
                Detail = status.Projection.Detail
            });

            status.Health = issues;
            return status;
        }
    }

    public List<FamilyHealthCheckStatus> BuildFamilyHealth() => BuildFamilyStatus().Health;

    public FamilyPerformanceStatus BuildFamilyPerformanceStatus()
    {
        using (WriteLock())
        {
            var snapshot = metrics.Snapshot();
            var counts = relationshipCache.Candidates.Values
                .Select(candidates => candidates.Count)
                .OrderBy(count => count)
                .ToList();
            var worst = snapshot.Systems
                .OrderByDescending(system => system.AverageMilliseconds)
                .FirstOrDefault();

            return new FamilyPerformanceStatus
            {
                LastTickMilliseconds = snapshot.LastTickMilliseconds,
                P95TickMilliseconds = snapshot.P95TickMilliseconds,
                P99TickMilliseconds = snapshot.P99TickMilliseconds,
                AllocatedBytesPerTick = snapshot.AllocatedBytesPerTick,
                CandidateGenoses = counts.Count,
                CandidateAverage = counts.Count > 0 ? (int)Math.Round(counts.Average()) : 0,
                CandidateP95 = Percentile(counts, 0.95),
                CandidateMax = counts.Count > 0 ? counts[^1] : 0,
                CandidateCap = relationshipOptions.MaxCandidatesPerGenos,
                WorstSystem = worst?.Name ?? string.Empty,
                WorstSystemAverageMilliseconds = worst?.AverageMilliseconds ?? 0
            };
        }
    }

    private static int Percentile(List<int> values, double percentile)
    {
        if (values.Count == 0)
            return 0;
        var rank = (int)Math.Ceiling(percentile * values.Count) - 1;
        return values[Math.Clamp(rank, 0, values.Count - 1)];
    }

    public TraitDistributionStatus BuildTraitDistribution()
    {
        using (WriteLock())
        {
            var result = new TraitDistributionStatus();
            if (world is null)
                return result;

            var distribution = TraitDistribution.Calculate(world);
            result.Population = distribution.Population;
            result.WithTraits = distribution.WithTraits;
            result.MissingTraits = distribution.MissingTraits;
            result.FemaleShare = distribution.FemaleShare;
            result.MutationEvents = 0;
            result.Migrated = 0;

            foreach (var category in distribution.Counts.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var total = category.Value.Values.Sum();
                result.Categories.Add(new TraitCategoryStatus
                {
                    Name = category.Key,
                    Values = category.Value
                        .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                        .Select(pair => new TraitValueStatus
                        {
                            Value = pair.Key,
                            Count = pair.Value,
                            Share = total > 0 ? pair.Value / (decimal)total : 0m
                        })
                        .ToList()
                });
            }

            return result;
        }
    }

    public List<HouseholdCompositionStatus> BuildHouseholdCompositions()
    {
        using (WriteLock())
        {
            var result = new List<HouseholdCompositionStatus>();
            if (world is null || economy is null)
                return result;

            var households = new List<Entity>();
            world.Query(in new QueryDescription().WithAll<Household>(),
                (Entity entity, ref Household _) => households.Add(entity));
            households.Sort((a, b) => GetNetworkId(world, a).CompareTo(GetNetworkId(world, b)));

            foreach (var householdEntity in households)
            {
                var household = world.Get<Household>(householdEntity);
                var members = economy.Housing.GetMembers(householdEntity);
                var children = 0;
                var dependants = 0;
                var earners = 0;
                foreach (var member in members)
                {
                    if (!world.IsAlive(member))
                        continue;
                    if (world.Has<Alive>(member) && world.Get<Alive>(member).Age < 18)
                        children++;
                    if (PregnancyService.IsDependent(world, member))
                        dependants++;
                    if (world.Has<Employee>(member))
                        earners++;
                }

                var unit = household.Unit != Entity.Null && world.IsAlive(household.Unit)
                    ? world.Get<HousingUnit>(household.Unit)
                    : default;
                result.Add(new HouseholdCompositionStatus
                {
                    NetworkId = GetNetworkId(world, householdEntity),
                    HeadNetworkId = GetNetworkId(world, household.Head),
                    Head = NameOf(household.Head),
                    Members = members.Count,
                    Children = children,
                    Dependants = dependants,
                    Earners = earners,
                    Capacity = household.Unit == Entity.Null ? 0 : unit.Capacity,
                    OccupantCount = household.Unit == Entity.Null ? 0 : unit.OccupantCount,
                    Overcrowded = household.Unit != Entity.Null && unit.IsOvercrowded,
                    UnitNetworkId = household.Unit == Entity.Null ? null : GetNetworkId(world, household.Unit),
                    Unit = household.Unit == Entity.Null ? string.Empty : $"Unit {GetNetworkId(world, household.Unit)}",
                    Tenure = household.Tenure.ToString()
                });
            }

            return result;
        }
    }

    private int relationshipsCount(Func<Entity, bool> predicate)
    {
        var count = 0;
        world!.Query(in new QueryDescription().WithAll<Alive>().WithNone<Dead>(),
            (Entity entity, ref Alive _) =>
            {
                if (predicate(entity))
                    count++;
            });
        return count;
    }

    public List<PregnancyStatusDto> BuildPregnancies() => BuildPregnancyPage().Pregnancies;

    public PregnancyPageStatus BuildPregnancyPage(bool activeOnly = false, int take = 500, int skip = 0)
    {
        using (WriteLock())
        {
            var page = new PregnancyPageStatus
            {
                ActiveOnly = activeOnly,
                Take = Math.Clamp(take, 1, 500),
                Skip = Math.Max(0, skip)
            };
            if (world is null || economy is null)
                return page;

            var pregnancies = new List<PregnancyStatusDto>();
            world.Query(in new QueryDescription().WithAll<Pregnancy, NetworkId>(),
                (Entity entity, ref Pregnancy pregnancy, ref NetworkId networkId) =>
                {
                    if (activeOnly &&
                        pregnancy.Status is not (PregnancyStatus.Active or PregnancyStatus.ReadyToBirth))
                        return;

                    pregnancies.Add(BuildPregnancyStatus(pregnancy, networkId.Value));
                });

            pregnancies.Sort((a, b) => a.NetworkId.CompareTo(b.NetworkId));
            page.Total = pregnancies.Count;
            page.Pregnancies = pregnancies.Skip(page.Skip).Take(page.Take).ToList();
            return page;
        }
    }

    public PregnancyStatusDto? BuildPregnancy(ulong pregnancyId)
    {
        using (WriteLock())
        {
            if (world is null || economy is null ||
                !networkIds.TryGetEntity(new NetworkId(pregnancyId), out var entity) ||
                !world.IsAlive(entity) || !world.Has<Pregnancy>(entity))
                return null;

            return BuildPregnancyStatus(world.Get<Pregnancy>(entity), pregnancyId);
        }
    }

    public List<PregnancyStatusDto> BuildPregnanciesForGenos(ulong genosId)
    {
        using (WriteLock())
        {
            var result = new List<PregnancyStatusDto>();
            if (world is null || economy is null ||
                !networkIds.TryGetEntity(new NetworkId(genosId), out var genos))
                return result;

            world.Query(in new QueryDescription().WithAll<Pregnancy, NetworkId>(),
                (Entity _, ref Pregnancy pregnancy, ref NetworkId networkId) =>
                {
                    if (pregnancy.CarryingParent != genos && pregnancy.OtherParent != genos)
                        return;
                    result.Add(BuildPregnancyStatus(pregnancy, networkId.Value));
                });

            result.Sort((a, b) => a.NetworkId.CompareTo(b.NetworkId));
            return result;
        }
    }

    private PregnancyStatusDto BuildPregnancyStatus(Pregnancy pregnancy, ulong networkId)
    {
        var remaining = Math.Max(
            0,
            (int)Math.Ceiling((pregnancy.DueTick - tick) / (double)economy!.Clock.TicksPerMonth));
        return new PregnancyStatusDto
        {
            NetworkId = networkId,
            CarryingParentNetworkId = NetworkIdOf(world!, pregnancy.CarryingParent),
            OtherParentNetworkId = NetworkIdOf(world!, pregnancy.OtherParent),
            CarryingParentName = NameOf(pregnancy.CarryingParent),
            OtherParentName = NameOf(pregnancy.OtherParent),
            ConceivedTick = pregnancy.ConceivedTick,
            DueTick = pregnancy.DueTick,
            MonthsRemaining = remaining,
            RiskBasisPoints = pregnancy.RiskBasisPoints,
            Status = pregnancy.Status.ToString(),
            Source = pregnancy.Source.ToString(),
            ChildrenExpected = pregnancy.ChildrenExpected,
            Complications = pregnancy.Complications,
            CareMonths = pregnancy.CareMonths,
            MissedCareMonths = pregnancy.MissedCareMonths,
            CarePaid = pregnancy.CarePaid.Units,
            CareArrears = pregnancy.CareArrears.Units,
            ResolvedTick = pregnancy.ResolvedTick
        };
    }

    public List<ChildStatusDto> BuildChildren() => BuildChildPage().Children;

    public ChildPageStatus BuildChildPage(
        string? ageBand = null,
        ulong? guardianId = null,
        int take = 500,
        int skip = 0)
    {
        using (WriteLock())
        {
            var page = new ChildPageStatus
            {
                AgeBand = ageBand,
                Take = Math.Clamp(take, 1, 500),
                Skip = Math.Max(0, skip)
            };
            if (world is null || economy is null)
                return page;

            var workAge = economy.Life.Policy.WorkStartAge;
            var children = new List<ChildStatusDto>();
            world.Query(in new QueryDescription().WithAll<Alive, NetworkId>().WithNone<Dead>(),
                (Entity entity, ref Alive alive, ref NetworkId networkId) =>
                {
                    if (alive.Age >= workAge)
                        return;
                    if (!MatchesAgeBand(alive.Age, ageBand))
                        return;

                    var guardian = relationshipIndex.GuardiansOf(entity).FirstOrNull();
                    if (guardianId is not null && NetworkIdOf(world, guardian) != guardianId.Value)
                        return;

                    children.Add(BuildChildStatus(entity, alive, networkId.Value));
                });

            children.Sort((a, b) => a.NetworkId.CompareTo(b.NetworkId));
            page.Total = children.Count;
            page.Children = children.Skip(page.Skip).Take(page.Take).ToList();
            return page;
        }
    }

    public ChildStatusDto? BuildChild(ulong childId)
    {
        using (WriteLock())
        {
            if (world is null || economy is null ||
                !networkIds.TryGetEntity(new NetworkId(childId), out var entity) ||
                !world.IsAlive(entity) || world.Has<Dead>(entity) ||
                !world.Has<Alive>(entity) ||
                world.Get<Alive>(entity).Age >= economy.Life.Policy.WorkStartAge)
                return null;

            return BuildChildStatus(entity, world.Get<Alive>(entity), childId);
        }
    }

    public List<ChildStatusDto> BuildChildrenForGenos(ulong genosId)
    {
        using (WriteLock())
        {
            var result = new List<ChildStatusDto>();
            if (world is null || economy is null ||
                !networkIds.TryGetEntity(new NetworkId(genosId), out var genos))
                return result;

            var workAge = economy.Life.Policy.WorkStartAge;
            world.Query(in new QueryDescription().WithAll<Alive, NetworkId>().WithNone<Dead>(),
                (Entity entity, ref Alive alive, ref NetworkId networkId) =>
                {
                    if (alive.Age >= workAge)
                        return;

                    var parents = relationshipIndex.ParentsOf(entity);
                    var guardian = relationshipIndex.GuardiansOf(entity).FirstOrNull();
                    if (!parents.Contains(genos) && guardian != genos)
                        return;

                    result.Add(BuildChildStatus(entity, alive, networkId.Value));
                });

            result.Sort((a, b) => a.NetworkId.CompareTo(b.NetworkId));
            return result;
        }
    }

    private ChildStatusDto BuildChildStatus(Entity entity, Alive alive, ulong networkId)
    {
        var parents = relationshipIndex.ParentsOf(entity);
        var guardian = relationshipIndex.GuardiansOf(entity).FirstOrNull();
        var household = economy!.Housing.HouseholdOf(entity);
        var householdId = household == Entity.Null ? 0 : GetNetworkId(world!, household);
        var enrolled = world!.Has<Enrollment>(entity) &&
                       !world.Get<Enrollment>(entity).IsUnenrolled;
        var unenrolled = world.Has<Enrollment>(entity) &&
                         world.Get<Enrollment>(entity).IsUnenrolled;
        var school = world.Has<Enrollment>(entity)
            ? world.Get<Enrollment>(entity).School
            : Entity.Null;
        var unit = household == Entity.Null ? Entity.Null : economy.Housing.UnitOf(household);
        var overcrowded = unit != Entity.Null && world.Has<HousingUnit>(unit) &&
                          world.Get<HousingUnit>(unit).IsOvercrowded;

        return new ChildStatusDto
        {
            NetworkId = networkId,
            Name = NameOf(entity),
            Age = alive.Age,
            ParentANetworkId = parents.Count > 0 ? NetworkIdOf(world, parents[0]) : null,
            ParentBNetworkId = parents.Count > 1 ? NetworkIdOf(world, parents[1]) : null,
            GuardianNetworkId = guardian == Entity.Null ? null : NetworkIdOf(world, guardian),
            HouseholdNetworkId = householdId,
            Household = householdId == 0 ? string.Empty : $"Household {householdId}",
            Enrolled = enrolled,
            Unenrolled = unenrolled,
            School = school == Entity.Null ? string.Empty : NameOf(school),
            Overcrowded = overcrowded,
            InStateCare = world.Has<StateCare>(entity),
            ChildCostsThisYear = 0m
        };
    }

    private static bool MatchesAgeBand(int age, string? ageBand)
    {
        if (string.IsNullOrWhiteSpace(ageBand))
            return true;

        return ageBand.Trim().ToLowerInvariant() switch
        {
            "infant" or "under-1" => age < 1,
            "toddler" or "1-4" => age is >= 1 and < 5,
            "school" or "5-12" => age is >= 5 and < 13,
            "teen" or "13-17" => age is >= 13 and < 18,
            _ => true
        };
    }

    public List<GuardianshipStatusDto> BuildGuardianships()
    {
        using (WriteLock())
        {
            var result = new List<GuardianshipStatusDto>();
            if (world is null)
                return result;

            world.Query(in new QueryDescription().WithAll<Relationship, NetworkId>(),
                (Entity _, ref Relationship relationship, ref NetworkId __) =>
                {
                    if (relationship.Kind != RelationshipKind.Guardian)
                        return;
                    result.Add(new GuardianshipStatusDto
                    {
                        GuardianNetworkId = NetworkIdOf(world, relationship.From),
                        GuardianName = NameOf(relationship.From),
                        ChildNetworkId = NetworkIdOf(world, relationship.To),
                        ChildName = NameOf(relationship.To),
                        Source = relationship.Source.ToString(),
                        SinceTick = relationship.SinceTick,
                        StateCare = false
                    });
                });

            world.Query(in new QueryDescription().WithAll<StateCare, NetworkId>(),
                (Entity entity, ref StateCare care, ref NetworkId networkId) =>
                {
                    result.Add(new GuardianshipStatusDto
                    {
                        GuardianNetworkId = 0,
                        GuardianName = "State care",
                        ChildNetworkId = networkId.Value,
                        ChildName = NameOf(entity),
                        Source = "StateCare",
                        SinceTick = care.SinceTick,
                        StateCare = true
                    });
                });

            result.Sort((a, b) => a.ChildNetworkId.CompareTo(b.ChildNetworkId));
            return result;
        }
    }

    public FamilyGenosPage BuildFamilyGenoses(
        int take = 100,
        int skip = 0,
        string? stage = null,
        bool? partnered = null,
        bool? hasChildren = null,
        ulong? householdId = null,
        string? name = null)
    {
        using (WriteLock())
        {
            var page = new FamilyGenosPage
            {
                Take = Math.Clamp(take, 1, 500),
                Skip = Math.Max(0, skip)
            };
            if (world is null || economy is null)
                return page;

            LifeStage? stageFilter = Enum.TryParse<LifeStage>(stage, true, out var parsedStage)
                ? parsedStage
                : null;
            var rows = new List<(ulong NetworkId, Entity Entity, Alive Alive, GenName Name, LifeStage Stage, Entity Household, ulong HouseholdId)>();
            world.Query(in new QueryDescription().WithAll<Alive, GenName, NetworkId>().WithNone<Dead>(),
                (Entity entity, ref Alive alive, ref GenName genosName, ref NetworkId networkId) =>
                {
                    var stageValue = world.Has<Lifecycle>(entity)
                        ? world.Get<Lifecycle>(entity).Stage
                        : economy.Life.Policy.StageFor(alive.Age);
                    if (stageFilter is { } required && stageValue != required)
                        return;
                    if (!string.IsNullOrWhiteSpace(name) &&
                        !genosName.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                        return;

                    var isPaired = relationshipIndex.HasPartner(entity);
                    if (partnered is not null && isPaired != partnered.Value)
                        return;

                    var childCount = relationshipIndex.ChildrenOf(entity).Count;
                    if (hasChildren is not null && (childCount > 0) != hasChildren.Value)
                        return;

                    var household = economy.Housing.HouseholdOf(entity);
                    var householdNetworkId = household == Entity.Null ? 0 : GetNetworkId(world, household);
                    if (householdId is not null && householdNetworkId != householdId.Value)
                        return;

                    rows.Add((networkId.Value, entity, alive, genosName, stageValue, household, householdNetworkId));
                });

            rows.Sort((a, b) => a.NetworkId.CompareTo(b.NetworkId));
            page.Total = rows.Count;
            page.Genoses = rows
                .Skip(page.Skip)
                .Take(page.Take)
                .Select(row => BuildFamilyGenosStatus(
                    row.Entity, row.Alive, row.Name, row.NetworkId, row.Stage, row.Household, row.HouseholdId))
                .ToList();
            return page;
        }
    }

    public FamilyGenosDetailStatus? BuildFamilyGenos(ulong genosId)
    {
        using (WriteLock())
        {
            if (world is null || economy is null ||
                !networkIds.TryGetEntity(new NetworkId(genosId), out var entity) ||
                !world.IsAlive(entity) || !world.Has<Alive>(entity))
                return null;

            var alive = world.Get<Alive>(entity);
            var stageValue = world.Has<Lifecycle>(entity)
                ? world.Get<Lifecycle>(entity).Stage
                : economy.Life.Policy.StageFor(alive.Age);
            var household = economy.Housing.HouseholdOf(entity);
            var householdNetworkId = household == Entity.Null ? 0 : GetNetworkId(world, household);
            var name = world.Has<GenName>(entity) ? world.Get<GenName>(entity).Name : string.Empty;

            var relationships = new List<FamilyRelationshipStatus>();
            world.Query(in new QueryDescription().WithAll<Relationship, NetworkId>(),
                (Entity _, ref Relationship relationship, ref NetworkId relationshipId) =>
                {
                    if (relationship.From != entity && relationship.To != entity)
                        return;
                    relationships.Add(new FamilyRelationshipStatus
                    {
                        NetworkId = relationshipId.Value,
                        FromGenosId = NetworkIdOf(world, relationship.From),
                        FromName = NameOf(relationship.From),
                        ToGenosId = NetworkIdOf(world, relationship.To),
                        ToName = NameOf(relationship.To),
                        Kind = relationship.Kind.ToString(),
                        Source = relationship.Source.ToString(),
                        SinceTick = relationship.SinceTick
                    });
                });
            relationships.Sort((a, b) => a.NetworkId.CompareTo(b.NetworkId));

            var detail = new FamilyGenosDetailStatus
            {
                Genos = BuildFamilyGenosStatus(entity, alive, new GenName(name), genosId, stageValue, household, householdNetworkId),
                Relationships = relationships,
                Pregnancies = BuildPregnanciesForGenos(genosId),
                Children = BuildChildrenForGenos(genosId),
                Guardianships = BuildGuardianships()
                    .Where(guardian => guardian.GuardianNetworkId == genosId || guardian.ChildNetworkId == genosId)
                    .ToList()
            };

            detail.Household = householdNetworkId == 0
                ? null
                : BuildHouseholdCompositions().FirstOrDefault(household => household.NetworkId == householdNetworkId);
            return detail;
        }
    }

    private FamilyGenosStatus BuildFamilyGenosStatus(
        Entity entity,
        Alive alive,
        GenName genosName,
        ulong networkId,
        LifeStage stage,
        Entity household,
        ulong householdNetworkId)
    {
        var partner = relationshipIndex.PartnersOf(entity).FirstOrNull();
        var guardian = relationshipIndex.GuardiansOf(entity).FirstOrNull();
        var parents = relationshipIndex.ParentsOf(entity);
        var children = relationshipIndex.ChildrenOf(entity);
        var isOrphan = alive.Age < economy!.Life.Policy.WorkStartAge &&
                       !world!.Has<StateCare>(entity) &&
                       !parents.Any(parent => world.IsAlive(parent) && !world.Has<Dead>(parent)) &&
                       !relationshipsAlive(guardian);

        var status = new FamilyGenosStatus
        {
            NetworkId = networkId,
            Name = genosName.Name,
            Age = alive.Age,
            Stage = stage.ToString(),
            Sex = world!.Has<Traits>(entity) ? world.Get<Traits>(entity).Sex.ToString() : string.Empty,
            HouseholdNetworkId = householdNetworkId,
            Household = householdNetworkId == 0 ? string.Empty : $"Household {householdNetworkId}",
            PartnerNetworkId = partner == Entity.Null ? null : NetworkIdOf(world, partner),
            PartnerName = partner == Entity.Null ? null : NameOf(partner),
            GuardianNetworkId = guardian == Entity.Null ? null : NetworkIdOf(world, guardian),
            IsPaired = partner != Entity.Null,
            HasChildren = children.Count > 0,
            IsOrphan = isOrphan,
            InStateCare = world.Has<StateCare>(entity),
            ParentCount = parents.Count,
            ChildCount = children.Count
        };

        if (world.Has<Traits>(entity))
        {
            var traits = world.Get<Traits>(entity);
            status.Traits =
            [
                new TraitValueStatus { Value = $"EyeColor={traits.EyeColor}", Count = 1 },
                new TraitValueStatus { Value = $"HairColor={traits.HairColor}", Count = 1 },
                new TraitValueStatus { Value = $"Handedness={traits.Handedness}", Count = 1 },
                new TraitValueStatus { Value = $"Morphotype={traits.Morphotype}", Count = 1 },
                new TraitValueStatus { Value = $"Aptitude={traits.Aptitude}", Count = 1 },
                new TraitValueStatus { Value = $"HeightPotential={traits.HeightPotential}", Count = 1 },
                new TraitValueStatus { Value = $"Sex={traits.Sex}", Count = 1 },
                new TraitValueStatus { Value = $"Temperament={traits.Temperament}", Count = 1 }
            ];
        }

        return status;

        bool relationshipsAlive(Entity candidate) =>
            candidate != Entity.Null && world!.IsAlive(candidate) && !world.Has<Dead>(candidate);
    }

    public FamilyRelationshipPage BuildFamilyRelationships(
        ulong? genosId = null,
        string? kind = null,
        string? source = null,
        int take = 100,
        int skip = 0)
    {
        using (WriteLock())
        {
            var page = new FamilyRelationshipPage
            {
                Take = Math.Clamp(take, 1, 500),
                Skip = Math.Max(0, skip)
            };
            if (world is null)
                return page;

            RelationshipKind? kindFilter = Enum.TryParse<RelationshipKind>(kind, true, out var parsedKind)
                ? parsedKind
                : null;
            RelationshipSource? sourceFilter = Enum.TryParse<RelationshipSource>(source, true, out var parsedSource)
                ? parsedSource
                : null;

            var relationships = new List<(ulong NetworkId, FamilyRelationshipStatus Status)>();
            world.Query(in new QueryDescription().WithAll<Relationship, NetworkId>(),
                (Entity entity, ref Relationship relationship, ref NetworkId networkId) =>
                {
                    if (kindFilter is not null && relationship.Kind != kindFilter.Value)
                        return;
                    if (sourceFilter is not null && relationship.Source != sourceFilter.Value)
                        return;

                    var fromId = NetworkIdOf(world, relationship.From);
                    var toId = NetworkIdOf(world, relationship.To);
                    if (genosId is not null && genosId.Value != fromId && genosId.Value != toId)
                        return;

                    relationships.Add((networkId.Value, new FamilyRelationshipStatus
                    {
                        NetworkId = networkId.Value,
                        FromGenosId = fromId,
                        FromName = NameOf(relationship.From),
                        ToGenosId = toId,
                        ToName = NameOf(relationship.To),
                        Kind = relationship.Kind.ToString(),
                        Source = relationship.Source.ToString(),
                        SinceTick = relationship.SinceTick
                    }));
                });

            relationships.Sort((a, b) => a.NetworkId.CompareTo(b.NetworkId));
            page.Total = relationships.Count;
            page.Relationships = relationships
                .Skip(page.Skip)
                .Take(page.Take)
                .Select(pair => pair.Status)
                .ToList();
            return page;
        }
    }

    private string NameOf(Entity entity) =>
        world is not null && world.IsAlive(entity) && world.Has<GenName>(entity)
            ? world.Get<GenName>(entity).Name
            : string.Empty;

    public AttractionExplanationStatus BuildAttractionExplanation(ulong genosId, ulong candidateId)
    {
        using (WriteLock())
        {
            var result = new AttractionExplanationStatus
            {
                GenosId = genosId,
                CandidateId = candidateId
            };
            if (world is null || economy is null ||
                !networkIds.TryGetEntity(new NetworkId(genosId), out var first) ||
                !networkIds.TryGetEntity(new NetworkId(candidateId), out var second))
            {
                result.ReasonCode = "not-found";
                result.Reason = "One or both genoses do not exist.";
                return result;
            }

            var eligibility = EligibilityPolicy.Evaluate(
                world,
                relationshipIndex,
                first,
                second,
                relationshipOptions,
                (a, b) => relationshipCache.InCooldown(a, b, tick));
            result.Eligible = eligibility.Eligible;
            result.ReasonCode = eligibility.Code;
            result.Reason = eligibility.Reason;
            if (!eligibility.Eligible || !world.Has<Traits>(first) || !world.Has<Traits>(second))
                return result;

            var input = AttractionInputFactory.Create(
                world,
                economy,
                first,
                second,
                !relationshipIndex.HasPartner(first) && !relationshipCache.InCooldown(first, second, tick),
                !relationshipIndex.HasPartner(second) && !relationshipCache.InCooldown(first, second, tick));
            var score = AttractionScorer.Score(input, eligibility, relationshipOptions);
            result.Score = score.Score;
            result.Components = score.Components
                .Select(component => new AttractionComponentStatus
                {
                    Name = component.Name,
                    Points = component.Points,
                    Max = component.Max
                })
                .ToList();
            return result;
        }
    }

    public List<GenosLifecycleStatus> BuildLifecycleGenoses(int take = 500, int skip = 0, string? stageFilter = null)
    {
        using (WriteLock())
        {
            var result = new List<GenosLifecycleStatus>();
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
                    var row = new GenosLifecycleStatus
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

                    if (economy.Housing.HouseholdByGenos.TryGetValue(entity, out var household))
                        row.HouseholdNetworkId = GetNetworkId(world, household);

                    if (world.Has<Traits>(entity))
                    {
                        var traits = world.Get<Traits>(entity);
                        row.HasTraits = true;
                        row.EyeColor = traits.EyeColor.ToString();
                        row.HairColor = traits.HairColor.ToString();
                        row.Handedness = traits.Handedness.ToString();
                        row.Morphotype = traits.Morphotype.ToString();
                        row.Aptitude = traits.Aptitude.ToString();
                        row.HeightPotential = traits.HeightPotential.ToString();
                        row.Sex = traits.Sex.ToString();
                        row.Temperament = traits.Temperament.ToString();
                    }

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
        using (WriteLock())
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
        using (WriteLock())
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
        using (WriteLock())
        {
            if (economy is null)
                return new List<MortalityRecordStatus>();

            return economy.Life.MortalityLog
                .OrderByDescending(record => record.Tick)
                .ThenByDescending(record => record.GenosNetworkId)
                .Take(Math.Clamp(take, 1, 1000))
                .Select(record => new MortalityRecordStatus
                {
                    Tick = record.Tick,
                    GenosNetworkId = record.GenosNetworkId,
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
        using (WriteLock())
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
        using (WriteLock())
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
        using (WriteLock())
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
        using (WriteLock())
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
        using (WriteLock())
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
        using (ReadLock())
        {
            return metrics.Snapshot();
        }
    }

    public T ExecuteWithWorld<T>(Func<World, T> operation)
    {
        using (WriteLock())
        {
            if (world == null)
                throw new InvalidOperationException("Simulation is not running");
            return operation(world);
        }
    }

    public bool TryGetEntity(NetworkId networkId, out Entity entity)
    {
        using (ReadLock())
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
        using (ReadLock())
        {
            return world != null && world.IsAlive(entity);
        }
    }

    public string? GetOwnerPlayerId(Entity entity)
    {
        using (ReadLock())
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
        using (WriteLock())
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
        using (WriteLock())
        {
            if (world == null || !world.IsAlive(entity) || !world.Has<PlayerControlled>(entity))
                return false;

            world.Remove<PlayerControlled>(entity);
            return true;
        }
    }

    private List<GenosStatus> BuildGenosSnapshots(World world)
    {
        var genoses = new List<GenosStatus>();
        var query = new QueryDescription().WithAll<GenName, Needs, CurrentAction>();

        world.Query(in query, (Entity entity, ref GenName name, ref Needs needs, ref CurrentAction action) =>
        {
            var isPlayerControlled = world.Has<PlayerControlled>(entity);
            var status = new GenosStatus
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

            status.Appearance = GetCachedAppearance(world, entity);

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

            genoses.Add(status);
        });

        return genoses;
    }

    /// <summary>
    /// Derives a genos's appearance only when its inputs changed. The derivation
    /// depends on traits, age band, appearance version and the stable key
    /// (network id), so all four are part of the cache stamp.
    /// </summary>
    private AppearanceDto? GetCachedAppearance(World world, Entity entity)
    {
        if (!world.Has<Traits>(entity) || !world.Has<Alive>(entity))
            return null;

        var traits = world.Get<Traits>(entity);
        var age = world.Get<Alive>(entity).Age;
        var band = AgingAppearancePolicy.BandFor(age);
        var version = AppearanceDescriptor.CurrentVersion;
        var stableKey = AppearanceMapper.StableKey(world, entity);

        if (appearanceCache.TryGetValue(entity, out var cached) &&
            cached.Version == version &&
            cached.Band == band &&
            cached.StableKey == stableKey &&
            cached.Traits.Equals(traits))
        {
            return cached.Dto;
        }

        if (!AppearanceMapper.TryDerive(world, entity, out var descriptor, version))
            return null;

        var dto = AppearanceMapper.ToDto(descriptor);
        appearanceCache[entity] = new AppearanceCacheEntry(traits, band, version, stableKey, dto);
        return dto;
    }

    private readonly record struct AppearanceCacheEntry(
        Traits Traits,
        AgeBand Band,
        byte Version,
        ulong StableKey,
        AppearanceDto Dto);

    private static List<CompanyStatus> BuildCompanySnapshots(World world, EmploymentIndex index)
    {
        var companies = new List<CompanyStatus>();
        var query = new QueryDescription().WithAll<CompanyName>();

        // One office pass per snapshot keeps this O(companies + offices) instead
        // of running an Office query inside the company loop.
        var officesByCompany = CompanyOfficeCounter.CountOpenByCompany(world);

        world.Query(in query, (Entity entity, ref CompanyName companyName) =>
        {
            var status = new CompanyStatus
            {
                NetworkId = world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : 0,
                Name = companyName.Name,
                EmployeeCount = index.CountEmployees(entity),
                OfficeCount = officesByCompany.GetValueOrDefault(entity)
            };

            if (world.Has<CompanyAccount>(entity))
            {
                var financials = CompanyFinancialSnapshot.From(world.Get<CompanyAccount>(entity));
                status.Balance = financials.Balance;
                status.ReservedForPayroll = financials.ReservedForPayroll;
                status.Arrears = financials.Arrears;
                status.RevenueLastCycle = financials.RevenueLastCycle;
                status.WagesLastCycle = financials.WagesLastCycle;
                status.Solvency = financials.Solvency;
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
        ulong GenosNetworkId,
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
        using (WriteLock())
        {
            if (world == null || economy == null)
                throw new InvalidOperationException("Simulation is not running");

            var currentTick = Volatile.Read(ref tick);
            var genoses = new List<PersistedGenos>();
            var companies = new List<PersistedCompany>();
            var offices = new List<PersistedOffice>();
            var postings = new List<PersistedPosting>();
            var applications = new List<PersistedApplication>();
            var loans = new List<PersistedLoan>();
            var pendingRevenue = new List<PersistedPendingRevenue>();
            var playerProfiles = new List<PersistedPlayerProfile>();
            var playerLives = new List<PersistedPlayerLife>();
            var familyInvitations = new List<PersistedFamilyInvitation>();
            var utilityNetworks = new List<PersistedUtilityNetwork>();
            var serviceLinks = new List<PersistedServiceLink>();
            var householdMeters = new List<PersistedHouseholdMeter>();
            var storeInventories = new List<PersistedStoreInventory>();
            var nutritionRows = new List<PersistedNutrition>();
            var genosByEntity = new Dictionary<Entity, PersistedGenos>();

            var genosQuery = new QueryDescription().WithAll<GenName, Needs, Alive, NetworkId>();
            world.Query(in genosQuery, (Entity entity, ref GenName name, ref Needs needs, ref Alive alive, ref NetworkId nid) =>
            {
                var action = world.Has<CurrentAction>(entity)
                    ? world.Get<CurrentAction>(entity)
                    : new CurrentAction(ActionType.Idle, 0f);
                var genos = new PersistedGenos
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
                genoses.Add(genos);
                genosByEntity[entity] = genos;
            });

            var traitsQuery = new QueryDescription().WithAll<GenName, Traits>();
            world.Query(in traitsQuery, (Entity entity, ref Traits traits) =>
            {
                if (!genosByEntity.TryGetValue(entity, out var genos))
                    return;
                genos.EyeColor = (int)traits.EyeColor;
                genos.HairColor = (int)traits.HairColor;
                genos.Handedness = (int)traits.Handedness;
                genos.Morphotype = (int)traits.Morphotype;
                genos.Aptitude = (int)traits.Aptitude;
                genos.HeightPotential = (int)traits.HeightPotential;
                genos.Sex = (int)traits.Sex;
                genos.Temperament = (int)traits.Temperament;
                genos.SkinTone = (int)traits.SkinTone;
                genos.FaceShape = (int)traits.FaceShape;
                genos.AppearanceVersion = AppearanceDescriptor.CurrentVersion;
                genos.AppearanceMigrated = true;
                genos.TraitsMigrated = true;
            });

            var birthRecords = new QueryDescription().WithAll<GenName, BirthRecord>();
            world.Query(in birthRecords, (Entity entity, ref GenName _, ref BirthRecord record) =>
            {
                if (!genosByEntity.TryGetValue(entity, out var genos))
                    return;
                genos.BirthPregnancyNetworkId = record.PregnancyNetworkId;
                genos.BirthTick = record.BornTick;
            });

            var jobSeekerQuery = new QueryDescription().WithAll<GenName, JobSeeker>();
            world.Query(in jobSeekerQuery, (Entity entity, ref JobSeeker js) =>
            {
                if (genosByEntity.TryGetValue(entity, out var genos))
                {
                    genos.IsLookingForWork = js.IsLookingForWork;
                    genos.DesiredSalaryCents = js.DesiredSalary.Cents;
                }
            });

            var locationQuery = new QueryDescription().WithAll<GenName, Location>();
            world.Query(in locationQuery, (Entity entity, ref Location location) =>
            {
                if (genosByEntity.TryGetValue(entity, out var genos))
                {
                    genos.X = location.Position.X;
                    genos.Y = location.Position.Y;
                }
            });

            var tileQuery = new QueryDescription().WithAll<GenName, TilePosition>();
            world.Query(in tileQuery, (Entity entity, ref TilePosition position) =>
            {
                if (genosByEntity.TryGetValue(entity, out var genos))
                {
                    genos.HasTilePosition = true;
                    genos.TileX = position.Tile.X;
                    genos.TileY = position.Tile.Y;
                }
            });

            var moveTargetQuery = new QueryDescription().WithAll<GenName, MoveTarget>();
            world.Query(in moveTargetQuery, (Entity entity, ref MoveTarget target) =>
            {
                if (genosByEntity.TryGetValue(entity, out var genos))
                {
                    genos.HasMoveTarget = target.HasTarget;
                    genos.MoveTargetX = target.Tile.X;
                    genos.MoveTargetY = target.Tile.Y;
                }
            });

            var movementQuery = new QueryDescription().WithAll<GenName, MovementState>();
            world.Query(in movementQuery, (Entity entity, ref MovementState movement) =>
            {
                if (genosByEntity.TryGetValue(entity, out var genos))
                {
                    genos.MoveFromX = movement.From.X;
                    genos.MoveFromY = movement.From.Y;
                    genos.MoveToX = movement.To.X;
                    genos.MoveToY = movement.To.Y;
                    genos.MoveProgress = movement.Progress;
                    genos.SubTileX = movement.IsMoving
                        ? movement.From.X + (movement.To.X - movement.From.X) * movement.Progress
                        : 0f;
                    genos.SubTileY = movement.IsMoving
                        ? movement.From.Y + (movement.To.Y - movement.From.Y) * movement.Progress
                        : 0f;
                }
            });

            var playerQuery = new QueryDescription().WithAll<GenName, PlayerControlled>();
            world.Query(in playerQuery, (Entity entity, ref PlayerControlled pc) =>
            {
                if (genosByEntity.TryGetValue(entity, out var genos))
                    genos.PlayerId = pc.PlayerId;
            });

            var walletQuery = new QueryDescription().WithAll<GenName, Wallet>();
            world.Query(in walletQuery, (Entity entity, ref Wallet wallet) =>
            {
                if (genosByEntity.TryGetValue(entity, out var genos))
                {
                    genos.HasWallet = true;
                    genos.WalletBalanceCents = wallet.Balance.Cents;
                }
            });

            var savingsQuery = new QueryDescription().WithAll<GenName, SavingsAccount>();
            world.Query(in savingsQuery, (Entity entity, ref SavingsAccount savings) =>
            {
                if (genosByEntity.TryGetValue(entity, out var genos))
                    genos.SavingsBalanceCents = savings.Balance.Cents;
            });

            var consumptionQuery = new QueryDescription().WithAll<GenName, ConsumptionState>();
            world.Query(in consumptionQuery, (Entity entity, ref ConsumptionState consumption) =>
            {
                if (genosByEntity.TryGetValue(entity, out var genos))
                {
                    genos.UnmetDemandCents = consumption.UnmetDemand.Cents;
                    genos.LastSpendCents = consumption.LastCycleSpent.Cents;
                    genos.ConsumptionLifetimeCents = consumption.LifetimeSpent.Cents;
                }
            });

            var pensionQuery = new QueryDescription().WithAll<GenName, PensionAccount>();
            world.Query(in pensionQuery, (Entity entity, ref PensionAccount pension) =>
            {
                if (genosByEntity.TryGetValue(entity, out var genos))
                {
                    genos.PensionEmployeeCents = pension.EmployeeContributions.Cents;
                    genos.PensionEmployerCents = pension.EmployerContributions.Cents;
                    genos.PensionCycles = pension.ContributingCycles;
                }
            });

            var employeeQuery = new QueryDescription().WithAll<Employee>();
            world.Query(in employeeQuery, (Entity entity, ref Employee employee) =>
            {
                if (!genosByEntity.TryGetValue(entity, out var genos))
                    return;
                genos.EmployerNetworkId = GetNetworkId(world, employee.Company);
                genos.OfficeNetworkId = GetNetworkId(world, employee.Office);
                genos.Position = employee.Position;
                genos.SalaryCents = employee.SalaryPerCycle.Cents;
                genos.Seniority = employee.Seniority.ToString();
                genos.HiredAtTick = employee.HiredAtTick;
            });

            var claimQuery = new QueryDescription().WithAll<WageClaim>();
            world.Query(in claimQuery, (Entity entity, ref WageClaim claim) =>
            {
                if (!genosByEntity.TryGetValue(entity, out var genos))
                    return;
                genos.ClaimCompanyNetworkId = GetNetworkId(world, claim.Company);
                genos.UnpaidGrossCents = claim.UnpaidGross.Cents;
                genos.UnpaidNetCents = claim.UnpaidNet.Cents;
                genos.SeveranceCents = claim.Severance.Cents;
                genos.ClaimCycles = claim.CyclesInArrears;
                genos.ClaimFirstTick = claim.FirstMissedTick;
            });

            var lifecycleQuery = new QueryDescription().WithAll<GenName, Lifecycle>();
            world.Query(in lifecycleQuery, (Entity entity, ref Lifecycle lifecycle) =>
            {
                if (!genosByEntity.TryGetValue(entity, out var genos))
                    return;
                genos.Stage = lifecycle.Stage.ToString();
                genos.AgeTicks = lifecycle.AgeTicks;
                genos.StageEnteredTick = lifecycle.StageEnteredTick;
                genos.StageCause = lifecycle.LastCause.ToString();
                genos.LastAgedTick = lifecycle.LastAgedTick;
                genos.MaxAgeFlagged = lifecycle.MaxAgeFlagged;
            });

            world.Query(in new QueryDescription().WithAll<GenName, SkillProfile>(),
                (Entity entity, ref SkillProfile profile) =>
                {
                    if (!genosByEntity.TryGetValue(entity, out var genos))
                        return;
                    genos.SkillProgramming = profile.Programming;
                    genos.SkillArt = profile.Art;
                    genos.SkillCooking = profile.Cooking;
                    genos.SkillMedical = profile.Medical;
                    genos.SkillEngineering = profile.Engineering;
                    genos.SkillSales = profile.Sales;
                });

            world.Query(in new QueryDescription().WithAll<GenName, Health>(),
                (Entity entity, ref Health health) =>
                {
                    if (!genosByEntity.TryGetValue(entity, out var genos))
                        return;
                    genos.HealthValue = health.Value;
                    genos.HealthDeclineRate = health.DeclineRate;
                    genos.HealthLastCareTick = health.LastCareTick;
                    genos.ChronicConditions = health.ChronicConditions;
                });

            world.Query(in new QueryDescription().WithAll<GenName, Enrollment>(),
                (Entity entity, ref Enrollment enrollment) =>
                {
                    if (!genosByEntity.TryGetValue(entity, out var genos))
                        return;
                    genos.EnrollmentSchoolNetworkId = GetNetworkId(world, enrollment.School);
                    genos.EnrollmentAttendance = enrollment.Attendance;
                    genos.EnrollmentMonths = enrollment.MonthsAttended;
                    genos.IsUnenrolled = enrollment.IsUnenrolled;
                });

            world.Query(in new QueryDescription().WithAll<GenName, EmploymentRecord>(),
                (Entity entity, ref EmploymentRecord record) =>
                {
                    if (!genosByEntity.TryGetValue(entity, out var sid))
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
                    if (!genosByEntity.TryGetValue(entity, out var sid))
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
                    if (!genosByEntity.TryGetValue(entity, out var sid))
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
                    if (!genosByEntity.TryGetValue(entity, out var sid))
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
                    if (!genosByEntity.TryGetValue(entity, out var sid))
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
                    company.RevenueThisCycleCents = account.RevenueThisCycle.Cents;
                    company.WagesThisCycleCents = account.WagesThisCycle.Cents;
                    company.CostsThisCycleCents = account.CostsThisCycle.Cents;
                    company.InterestPaidThisCycleCents = account.InterestPaidThisCycle.Cents;
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
                BankruptCompanies = snapshot.BankruptCompanies,
                CompanyFailureRate = snapshot.CompanyFailureRate
            }).ToList();

            foreach (var (company, revenue) in economy.PendingRevenue)
            {
                var companyNetworkId = GetNetworkId(world, company);
                foreach (var spend in revenue.Spends)
                {
                    pendingRevenue.Add(new PersistedPendingRevenue
                    {
                        CompanyNetworkId = companyNetworkId,
                        GenosNetworkId = spend.GenosNetworkId,
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
                        GenosNetworkId = GetNetworkId(world, entity),
                        Role = member.Role.ToString()
                    });
                });

            var relationships = new List<PersistedRelationship>();
            world.Query(in new QueryDescription().WithAll<Relationship>(),
                (Entity entity, ref Relationship relationship) =>
                {
                    var networkId = GetNetworkId(world, entity);
                    var fromId = GetNetworkId(world, relationship.From);
                    var toId = GetNetworkId(world, relationship.To);
                    if (networkId == 0 || fromId == 0 || toId == 0)
                        return;

                    relationships.Add(new PersistedRelationship
                    {
                        NetworkId = networkId,
                        FromGenosNetworkId = fromId,
                        ToGenosNetworkId = toId,
                        Kind = relationship.Kind.ToString(),
                        Source = relationship.Source.ToString(),
                        SinceTick = relationship.SinceTick,
                        EndedTick = relationship.Kind == RelationshipKind.ExPartner
                            ? relationship.SinceTick
                            : null
                    });
                });

            var pregnancies = new List<PersistedPregnancy>();
            world.Query(in new QueryDescription().WithAll<Pregnancy, NetworkId>(),
                (Entity entity, ref Pregnancy pregnancy, ref NetworkId nid) =>
                {
                    var carryingId = GetNetworkId(world, pregnancy.CarryingParent);
                    var otherId = GetNetworkId(world, pregnancy.OtherParent);
                    if (carryingId == 0 || otherId == 0)
                        return;

                    pregnancies.Add(new PersistedPregnancy
                    {
                        NetworkId = nid.Value,
                        CarryingParentNetworkId = carryingId,
                        OtherParentNetworkId = otherId,
                        ConceivedTick = pregnancy.ConceivedTick,
                        DueTick = pregnancy.DueTick,
                        RiskBasisPoints = pregnancy.RiskBasisPoints,
                        HealthAtConception = pregnancy.HealthAtConception,
                        Status = pregnancy.Status.ToString(),
                        Source = pregnancy.Source.ToString(),
                        LastProgressTick = pregnancy.LastProgressTick,
                        ChildrenExpected = pregnancy.ChildrenExpected,
                        Complications = pregnancy.Complications,
                        CarePaidCents = pregnancy.CarePaid.Cents,
                        CareArrearsCents = pregnancy.CareArrears.Cents,
                        CareMonths = pregnancy.CareMonths,
                        MissedCareMonths = pregnancy.MissedCareMonths,
                        ResolvedTick = pregnancy.ResolvedTick
                    });
                });

            var persistedTrusts = new List<PersistedTrust>();
            world.Query(in new QueryDescription().WithAll<TrustAccount, NetworkId>(),
                (Entity entity, ref TrustAccount trust, ref NetworkId networkId) =>
                {
                    persistedTrusts.Add(new PersistedTrust
                    {
                        NetworkId = networkId.Value,
                        BeneficiaryNetworkId = GetNetworkId(world, trust.Beneficiary),
                        GuardianNetworkId = GetNetworkId(world, trust.Guardian),
                        BalanceCents = trust.Balance.Cents,
                        CreatedTick = trust.CreatedTick,
                        ReleaseAgeTicks = trust.ReleaseAgeTicks,
                        SourceGenosNetworkId = trust.SourceGenosNetworkId
                    });
                });

            var persistedStateCare = new List<PersistedStateCare>();
            world.Query(in new QueryDescription().WithAll<StateCare, NetworkId>(),
                (Entity entity, ref StateCare care, ref NetworkId networkId) =>
                {
                    persistedStateCare.Add(new PersistedStateCare
                    {
                        ChildNetworkId = networkId.Value,
                        SinceTick = care.SinceTick,
                        MonthlyCostCents = care.MonthlyCost.Cents
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

            foreach (var (genosEntity, genos) in genosByEntity)
            {
                if (!economy.Housing.HouseholdByGenos.TryGetValue(genosEntity, out var householdEntity))
                    continue;
                if (!world.IsAlive(householdEntity) || !world.Has<Household>(householdEntity))
                    continue;

                var household = world.Get<Household>(householdEntity);
                genos.HouseholdNetworkId = GetNetworkId(world, householdEntity);
                genos.HouseholdUnitNetworkId = GetNetworkId(world, household.Unit);
                genos.Tenure = household.Tenure.ToString();
                if (world.IsAlive(household.Unit) && world.Has<HousingUnit>(household.Unit))
                    genos.HousingTier = world.Get<HousingUnit>(household.Unit).Tier.ToString();
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
                }).ToList(),
                BirthsThisYear = snapshot.BirthsThisYear,
                BirthRate = snapshot.BirthRate,
                NaturalChange = snapshot.NaturalChange,
                NetMigration = snapshot.NetMigration,
                ReplacementRate = snapshot.ReplacementRate,
                Children = snapshot.Children,
                ChildrenUnderSchoolAge = snapshot.ChildrenUnderSchoolAge,
                AverageHouseholdSize = snapshot.AverageHouseholdSize,
                Orphans = snapshot.Orphans,
                StateCareChildren = snapshot.StateCareChildren,
                GuardianCoverage = snapshot.GuardianCoverage,
                MinorTrustTotalCents = snapshot.MinorTrustTotal.Cents,
                InheritancePaidCents = snapshot.InheritancePaidThisYear.Cents,
                TrustReleasedCents = snapshot.TrustReleasedThisYear.Cents
            }).ToList();

            var mortalityLog = economy.Life.MortalityLog.Select(record => new PersistedMortalityRecord
            {
                Tick = record.Tick,
                GenosNetworkId = record.GenosNetworkId,
                Name = record.Name,
                Age = record.Age,
                Cause = record.Cause.ToString(),
                Wealth = record.Wealth
            }).ToList();

            var estateQueue = economy.Life.EstateQueue.Select(request => new PersistedEstateRequest
            {
                GenosNetworkId = request.GenosNetworkId,
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

            foreach (var profile in playerRegistry.All().OrderBy(p => p.PlayerId, StringComparer.Ordinal))
            {
                playerProfiles.Add(new PersistedPlayerProfile
                {
                    PlayerId = profile.PlayerId,
                    GenosNetworkId = profile.LastGenosNetworkId,
                    ContinuationPolicy = profile.ContinuationPolicy.ToString(),
                    AwaitingContinuation = profile.AwaitingContinuation,
                    Observer = profile.Observer,
                    CreatedTick = profile.CreatedTick,
                    LastActionTick = profile.LastActionTick,
                    LastSequence = profile.LastSequence,
                    LastGenosName = profile.LastGenosName,
                    LastGenosAge = profile.LastGenosAge,
                    LastGenosTraits = profile.LastGenosTraits,
                    LastGenosHouseholdNetworkId = profile.LastGenosHouseholdNetworkId,
                    LastGenosDeathTick = profile.LastGenosDeathTick,
                    PendingRespawnGenosNetworkId = profile.PendingRespawnGenosNetworkId
                });

                var genos = profile.ControlledGenos;
                if (genos == Entity.Null || !world.IsAlive(genos) || !world.Has<NetworkId>(genos))
                    continue;

                var life = new PersistedPlayerLife
                {
                    GenosNetworkId = world.Get<NetworkId>(genos).Value
                };

                if (world.Has<PlayerEmployment>(genos))
                {
                    var employment = world.Get<PlayerEmployment>(genos);
                    life.HasEmployment = true;
                    life.EmploymentOfficeNetworkId = GetNetworkId(world, employment.Office);
                    life.HiredTick = employment.HiredTick;
                    life.LastAttendedTick = employment.LastAttendedTick;
                    life.ConsecutiveMissedShifts = employment.ConsecutiveMissedShifts;
                    life.EmploymentSource = employment.Source.ToString();
                    life.AttendedThisMonth = employment.AttendedThisMonth;
                }

                if (world.Has<PlayerHousing>(genos))
                {
                    var lease = world.Get<PlayerHousing>(genos);
                    life.HouseholdNetworkId = GetNetworkId(world, lease.Household);
                    life.UnitNetworkId = GetNetworkId(world, lease.Unit);
                    life.LeaseStartTick = lease.LeaseStartTick;
                    life.DepositCents = lease.Deposit.Cents;
                    life.LeaseActive = lease.LeaseActive;
                }

                if (world.Has<PlayerJobOffer>(genos))
                {
                    var offer = world.Get<PlayerJobOffer>(genos);
                    life.JobOfferId = offer.OfferId;
                    life.JobOfferPostingNetworkId = GetNetworkId(world, offer.Posting);
                    life.JobOfferOfficeNetworkId = GetNetworkId(world, offer.Office);
                    life.JobOfferPosition = offer.Position;
                    life.JobOfferSalaryCents = offer.SalaryPerCycle.Cents;
                    life.JobOfferCreatedTick = offer.CreatedTick;
                    life.JobOfferExpiresTick = offer.ExpiresTick;
                }

                if (world.Has<PlayerLeaseOffer>(genos))
                {
                    var offer = world.Get<PlayerLeaseOffer>(genos);
                    life.LeaseOfferId = offer.OfferId;
                    life.LeaseOfferUnitNetworkId = GetNetworkId(world, offer.Unit);
                    life.LeaseOfferPropertyNetworkId = GetNetworkId(world, offer.Property);
                    life.LeaseOfferRentCents = offer.MonthlyRent.Cents;
                    life.LeaseOfferCreatedTick = offer.CreatedTick;
                    life.LeaseOfferExpiresTick = offer.ExpiresTick;
                }

                var pantryOwner = genos;
                if (life.HouseholdNetworkId != 0 &&
                    networkIds.TryGetEntity(new NetworkId(life.HouseholdNetworkId), out var pantryHousehold) &&
                    world.IsAlive(pantryHousehold))
                {
                    pantryOwner = pantryHousehold;
                }
                if (world.IsAlive(pantryOwner) && world.Has<HouseholdPantry>(pantryOwner))
                {
                    var pantry = world.Get<HouseholdPantry>(pantryOwner);
                    life.FoodUnits = pantry.FoodUnits;
                    life.HouseholdGoods = pantry.HouseholdGoods;
                    life.LastPurchaseTick = pantry.LastPurchaseTick;
                }

                playerLives.Add(life);
            }

            world.Query(in new QueryDescription().WithAll<FamilyInvitation, NetworkId>(),
                (Entity entity, ref FamilyInvitation invitation, ref NetworkId nid) =>
                {
                    familyInvitations.Add(new PersistedFamilyInvitation
                    {
                        NetworkId = nid.Value,
                        Id = invitation.Id,
                        FromGenosNetworkId = GetNetworkId(world, invitation.From),
                        ToGenosNetworkId = GetNetworkId(world, invitation.To),
                        Kind = invitation.Kind.ToString(),
                        Status = invitation.Status.ToString(),
                        CreatedTick = invitation.CreatedTick,
                        ExpiresTick = invitation.ExpiresTick
                    });
                });

            world.Query(in new QueryDescription().WithAll<UtilityNetwork, NetworkId>(),
                (ref UtilityNetwork network, ref NetworkId nid) =>
                {
                    utilityNetworks.Add(new PersistedUtilityNetwork
                    {
                        NetworkId = nid.Value,
                        Service = network.Service.ToString(),
                        Production = network.Production,
                        Capacity = network.Capacity,
                        Load = network.Load,
                        Reserve = network.Reserve,
                        State = network.State.ToString()
                    });
                });

            world.Query(in new QueryDescription().WithAll<BuildingServiceLink, ServiceLinkOwner>(),
                (Entity entity, ref BuildingServiceLink link, ref ServiceLinkOwner owner) =>
                {
                    serviceLinks.Add(new PersistedServiceLink
                    {
                        NetworkId = GetNetworkId(world, entity),
                        BuildingNetworkId = GetNetworkId(world, owner.Building),
                        UtilityNetworkId = GetNetworkId(world, link.Network),
                        Service = link.Service.ToString(),
                        Demand = link.Demand,
                        Connected = link.Connected,
                        Priority = link.Priority
                    });
                });

            world.Query(in new QueryDescription().WithAll<HouseholdMeter>(),
                (Entity entity, ref HouseholdMeter meter) =>
                {
                    householdMeters.Add(new PersistedHouseholdMeter
                    {
                        HouseholdNetworkId = GetNetworkId(world, entity),
                        WaterUsage = meter.WaterUsage,
                        ElectricityUsage = meter.ElectricityUsage,
                        WaterLevel = meter.WaterLevel.ToString(),
                        ElectricityLevel = meter.ElectricityLevel.ToString(),
                        ArrearsCents = meter.Arrears.Cents,
                        LastReadingTick = meter.LastReadingTick,
                        ConsecutivePoorMonths = meter.ConsecutivePoorMonths,
                        LastCutOffTick = meter.LastCutOffTick
                    });
                });

            world.Query(in new QueryDescription().WithAll<StoreInventory, StoreStockLink>(),
                (Entity entity, ref StoreInventory inventory, ref StoreStockLink link) =>
                {
                    storeInventories.Add(new PersistedStoreInventory
                    {
                        NetworkId = GetNetworkId(world, entity),
                        StoreNetworkId = GetNetworkId(world, link.Store),
                        ItemIndex = inventory.ItemIndex,
                        Stock = inventory.Stock,
                        CurrentPriceCents = inventory.CurrentPrice.Cents,
                        LastRestockTick = inventory.LastRestockTick
                    });
                });

            world.Query(in new QueryDescription().WithAll<NutritionState>(),
                (Entity entity, ref NutritionState nutrition) =>
                {
                    nutritionRows.Add(new PersistedNutrition
                    {
                        GenosNetworkId = GetNetworkId(world, entity),
                        Balance = nutrition.Balance,
                        ConsecutivePoorDays = nutrition.ConsecutivePoorDays,
                        LastMealTick = nutrition.LastMealTick
                    });
                });

            var socialSnapshot = socialGraph.Export();
            var socialEdges = socialSnapshot.Edges
                .Select(edge => new PersistedSocialEdge
                {
                    FromPlayerId = edge.FromPlayerId,
                    ToPlayerId = edge.ToPlayerId,
                    Kind = edge.Kind,
                    SinceTick = edge.SinceTick,
                    Source = edge.Source
                })
                .ToList();
            var socialRequests = socialSnapshot.Requests
                .Select(request => new PersistedSocialRequest
                {
                    Id = request.Id,
                    FromPlayerId = request.FromPlayerId,
                    ToPlayerId = request.ToPlayerId,
                    Kind = request.Kind,
                    Status = request.Status,
                    CreatedTick = request.CreatedTick,
                    ExpiresTick = request.ExpiresTick
                })
                .ToList();
            var socialRestrictions = socialSnapshot.Restrictions
                .Select(restriction => new PersistedSocialRestriction
                {
                    ActionId = restriction.ActionId,
                    PlayerId = restriction.PlayerId,
                    UntilTick = restriction.UntilTick,
                    ChatBlocked = restriction.ChatBlocked,
                    ActivityBlocked = restriction.ActivityBlocked,
                    InvitationsBlocked = restriction.InvitationsBlocked,
                    Reason = restriction.Reason,
                    CreatedTick = restriction.CreatedTick
                })
                .ToList();
            if (socialPrivacy is not null)
            {
                foreach (var profile in playerRegistry.All())
                    socialPrivacy.For(profile.PlayerId);
            }
            var socialSettings = socialPrivacy?.Export()
                .Select(setting => new PersistedSocialSettings
                {
                    PlayerId = setting.PlayerId,
                    ShowDisplayName = setting.ShowDisplayName,
                    AllowNearbyForNonFriends = setting.AllowNearbyForNonFriends,
                    FriendsOnlyMessages = setting.FriendsOnlyMessages
                })
                .ToList() ?? [];
            var relationshipInvitations = playerRelationships?.ExportInvitations()
                .Select(invitation => new PersistedPlayerRelationshipInvitation
                {
                    Id = invitation.Id,
                    FromPlayerId = invitation.FromPlayerId,
                    ToPlayerId = invitation.ToPlayerId,
                    Kind = invitation.Kind.ToString(),
                    Status = invitation.Status.ToString(),
                    CreatedTick = invitation.CreatedTick,
                    ExpiresTick = invitation.ExpiresTick
                })
                .ToList() ?? [];

            return new PersistedWorldState
            {
                SchemaVersion = PersistedWorldState.CurrentSchemaVersion,
                Tick = currentTick,
                RandomSeed = economy.Random.Seed,
                BalanceBackfilled = true,
                EconomyOptions = economy.Options,
                HousingOptions = economy.HousingOptions,
                GeneticsOptions = economy.Genetics,
                RelationshipOptions = relationshipOptions,
                ReproductionOptions = reproductionOptions,
                FamilyHousingPolicy = familyHousingPolicy,
                InheritanceOptions = inheritanceOptions,
                GuardianshipOptions = guardianshipOptions,
                AttractionModelVersion = AttractionCoefficients.CurrentVersion,
                Map = mapState,
                Placements = persistedPlacements,
                HousingMigrated = true,
                LifecycleMigrated = true,
                Schools = schools,
                LifecycleMetricHistory = lifecycleMetrics,
                MortalityLog = mortalityLog,
                EstateQueue = estateQueue,
                Immigration = immigration,
                PlayerProfiles = playerProfiles,
                PlayerLives = playerLives,
                FamilyInvitations = familyInvitations,
                SocialEdges = socialEdges,
                SocialRequests = socialRequests,
                SocialSettings = socialSettings,
                SocialRestrictions = socialRestrictions,
                PlayerRelationshipInvitations = relationshipInvitations,
                UtilityNetworks = utilityNetworks,
                ServiceLinks = serviceLinks,
                HouseholdMeters = householdMeters,
                StoreInventories = storeInventories,
                Nutrition = nutritionRows,
                GroceryCatalogVersion = GroceryCatalog.Version,
                Genoses = genoses,
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
                Relationships = relationships,
                Pregnancies = pregnancies,
                Trusts = persistedTrusts,
                StateCare = persistedStateCare,
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
                    ConsumptionThisYearCents = economy.ConsumptionThisYear.Cents,
                    CompaniesFailedThisYear = economy.CompaniesFailedThisYear
                }
            };
        }
    }

    private TileCoord ResolveRestoredTile(PersistedGenos genos)
    {
        if (mapContext is null)
            return new TileCoord(genos.TileX, genos.TileY);

        var tile = genos.HasTilePosition
            ? new TileCoord(genos.TileX, genos.TileY)
            : mapContext.Options.ToTile(genos.X, genos.Y);

        if (!mapContext.Options.Bounds.Contains(tile) || !mapContext.Map.IsWalkable(tile))
            tile = MapSpawner.FindSpawn(mapContext, genos.NetworkId);

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
        using (WriteLock())
        {
            if (isRunning)
                throw new InvalidOperationException("Simulation is already running. Stop it before restoring.");

            if (state.SchemaVersion != PersistedWorldState.CurrentSchemaVersion)
                throw new InvalidOperationException(
                    $"Cannot restore schema version {state.SchemaVersion}; expected {PersistedWorldState.CurrentSchemaVersion}");

            RandomSeed = state.RandomSeed == 0 ? 1 : state.RandomSeed;
            Initialize(
                state.EconomyOptions,
                state.HousingOptions,
                state.GeneticsOptions,
                state.RelationshipOptions,
                state.ReproductionOptions,
                state.FamilyHousingPolicy,
                state.InheritanceOptions,
                state.GuardianshipOptions);

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

            var genosEntities = new Dictionary<ulong, Entity>();
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
                account.RevenueThisCycle = new Money(company.RevenueThisCycleCents);
                account.WagesThisCycle = new Money(company.WagesThisCycleCents);
                account.CostsThisCycle = new Money(company.CostsThisCycleCents);
                account.InterestPaidThisCycle = new Money(company.InterestPaidThisCycleCents);
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
            foreach (var genos in state.Genoses)
            {
                var stage = Enum.TryParse<LifeStage>(genos.Stage, out var savedStage)
                    ? savedStage
                    : LifecycleClassifier.Classify(genos.Age, economy.Life.Policy);
                var ageTicks = genos.AgeTicks > 0
                    ? genos.AgeTicks
                    : LifecycleFactory.Create(genos.Age, restoredTick, clock, economy.Life.Policy, genos.NetworkId.GetHashCode()).AgeTicks;
                var cause = Enum.TryParse<StageTransitionCause>(genos.StageCause, out var savedCause)
                    ? savedCause
                    : StageTransitionCause.Restored;
                var lastAgedTick = !state.LifecycleMigrated && genos.LastAgedTick == 0
                    ? restoredTick
                    : genos.LastAgedTick;

                var profile = new SkillProfile
                {
                    Programming = genos.SkillProgramming,
                    Art = genos.SkillArt,
                    Cooking = genos.SkillCooking,
                    Medical = genos.SkillMedical,
                    Engineering = genos.SkillEngineering,
                    Sales = genos.SkillSales
                };

                Traits traits;
                if (!genos.TraitsMigrated)
                {
                    traits = TraitsGenerator.Create(
                        RandomSeed, genos.NetworkId, TraitSource.Migration, economy.Genetics);
                    TraitValidation.EnsureValid(traits, genos.NetworkId);
                    // Persist the generated values on the in-memory state so a second
                    // restore of the same object cannot backfill a different trait set.
                    genos.EyeColor = (int)traits.EyeColor;
                    genos.HairColor = (int)traits.HairColor;
                    genos.Handedness = (int)traits.Handedness;
                    genos.Morphotype = (int)traits.Morphotype;
                    genos.Aptitude = (int)traits.Aptitude;
                    genos.HeightPotential = (int)traits.HeightPotential;
                    genos.Sex = (int)traits.Sex;
                    genos.Temperament = (int)traits.Temperament;
                    genos.SkinTone = (int)traits.SkinTone;
                    genos.FaceShape = (int)traits.FaceShape;
                    genos.AppearanceVersion = AppearanceDescriptor.CurrentVersion;
                    genos.AppearanceMigrated = true;
                    genos.TraitsMigrated = true;
                }
                else
                {
                    if (!genos.AppearanceMigrated)
                    {
                        var (skin, face) = AppearanceDerivation.BackfillGenes(
                            RandomSeed, genos.NetworkId, AppearanceDescriptor.CurrentVersion);
                        genos.SkinTone = (int)skin;
                        genos.FaceShape = (int)face;
                        genos.AppearanceVersion = AppearanceDescriptor.CurrentVersion;
                        genos.AppearanceMigrated = true;
                        economy?.Emit(new AppearanceMigrated(
                            genos.NetworkId,
                            AppearanceDescriptor.CurrentVersion,
                            genos.SkinTone,
                            genos.FaceShape,
                            0));
                    }

                    if (genos.AppearanceVersion > AppearanceDescriptor.CurrentVersion)
                    {
                        throw new InvalidOperationException(
                            $"Genos {genos.NetworkId} has appearance version {genos.AppearanceVersion}, " +
                            $"newer than supported version {AppearanceDescriptor.CurrentVersion}.");
                    }

                    traits = new Traits
                    {
                        EyeColor = (EyeColor)genos.EyeColor,
                        HairColor = (HairColor)genos.HairColor,
                        Handedness = (Handedness)genos.Handedness,
                        Morphotype = (Morphotype)genos.Morphotype,
                        Aptitude = (Aptitude)genos.Aptitude,
                        HeightPotential = (HeightPotential)genos.HeightPotential,
                        Sex = (Sex)genos.Sex,
                        Temperament = (Temperament)genos.Temperament,
                        SkinTone = (SkinTone)genos.SkinTone,
                        FaceShape = (FaceShape)genos.FaceShape
                    };
                    TraitValidation.EnsureValid(traits, genos.NetworkId);

                    var descriptor = AppearanceDerivation.Derive(
                        traits,
                        genos.Age,
                        genos.NetworkId,
                        (byte)genos.AppearanceVersion);
                    AppearanceValidation.EnsureValid(descriptor, $"network id {genos.NetworkId}");
                }

                var spawnTile = ResolveRestoredTile(genos);
                var (spawnX, spawnY) = mapContext is not null
                    ? mapContext.Options.ToWorld(spawnTile)
                    : (genos.X, genos.Y);

                var entity = world!.Create(
                    new GenName(genos.Name),
                    new Needs { Hunger = genos.Hunger, Energy = genos.Energy, Hygiene = genos.Hygiene, Bladder = genos.Bladder },
                    new CurrentAction(
                        Enum.TryParse<ActionType>(genos.ActionType, out var at) ? at : ActionType.Idle,
                        genos.TimeRemaining),
                    new Alive(genos.Age),
                    new Location(spawnX, spawnY),
                    profile,
                    new Health(genos.HealthValue <= 0f ? HealthSystem.InitialForAge(genos.Age) : genos.HealthValue)
                    {
                        DeclineRate = genos.HealthDeclineRate,
                        LastCareTick = genos.HealthLastCareTick,
                        ChronicConditions = genos.ChronicConditions
                    },
                    new Lifecycle(stage, ageTicks, genos.StageEnteredTick, cause, lastAgedTick)
                    {
                        MaxAgeFlagged = genos.MaxAgeFlagged
                    },
                    traits
                );

                var walletCents = genos.WalletBalanceCents != 0
                    ? genos.WalletBalanceCents
                    : (long)Math.Round((decimal)genos.WalletBalance * 100m);
                if (genos.HasWallet || walletCents != 0 || genos.EmployerNetworkId != 0)
                    world.Add(entity, new Wallet(new Money(walletCents)));

                if (genos.SavingsBalanceCents != 0)
                    world.Add(entity, new SavingsAccount(new Money(genos.SavingsBalanceCents)));

                if (genos.UnmetDemandCents != 0 || genos.LastSpendCents != 0 || genos.ConsumptionLifetimeCents != 0)
                {
                    world.Add(entity, new ConsumptionState
                    {
                        UnmetDemand = new Money(genos.UnmetDemandCents),
                        LastCycleSpent = new Money(genos.LastSpendCents),
                        LifetimeSpent = new Money(genos.ConsumptionLifetimeCents),
                        LastSpendTick = state.Tick
                    });
                }

                if (genos.PensionEmployeeCents != 0 || genos.PensionEmployerCents != 0)
                {
                    world.Add(entity, new PensionAccount
                    {
                        EmployeeContributions = new Money(genos.PensionEmployeeCents),
                        EmployerContributions = new Money(genos.PensionEmployerCents),
                        ContributingCycles = genos.PensionCycles
                    });
                }

                var networkId = new NetworkId(genos.NetworkId);
                world.Add(entity, networkId);
                networkIds.AllocateWithValue(entity, networkId);
                genosEntities[genos.NetworkId] = entity;

                if (genos.BirthPregnancyNetworkId != 0)
                {
                    world.Add(entity, new BirthRecord
                    {
                        PregnancyNetworkId = genos.BirthPregnancyNetworkId,
                        BornTick = genos.BirthTick
                    });
                }

                if (mapContext is not null)
                {
                    world.Add(entity, new TilePosition(spawnTile));
                    if (genos.HasMoveTarget)
                        world.Add(entity, new MoveTarget(new TileCoord(genos.MoveTargetX, genos.MoveTargetY)));

                    var movement = new MovementState(MovementOptions.PlayerSpeedTilesPerSecond);
                    if (genos.MoveProgress > 0f || genos.MoveToX != 0 || genos.MoveToY != 0)
                    {
                        movement.From = new TileCoord(genos.MoveFromX, genos.MoveFromY);
                        movement.To = new TileCoord(genos.MoveToX, genos.MoveToY);
                        movement.Progress = genos.MoveProgress;
                        movement.IsMoving = genos.MoveProgress > 0f;
                    }
                    world.Add(entity, movement);
                }

                if (genos.IsLookingForWork)
                    world.Add(entity, new JobSeeker(
                        true,
                        profile.ToMask(),
                        genos.DesiredSalaryCents > 0 ? new Money(genos.DesiredSalaryCents) : Money.FromUnits(1000m)));

                if (genos.PlayerId != null)
                    world.Add(entity, new PlayerControlled(genos.PlayerId));

                if (genos.EnrollmentSchoolNetworkId != 0 || genos.IsUnenrolled)
                {
                    var schoolEntity = genos.EnrollmentSchoolNetworkId != 0 &&
                                       schoolEntities.TryGetValue(genos.EnrollmentSchoolNetworkId, out var school)
                        ? school
                        : Entity.Null;
                    world.Add(entity, new Enrollment(
                        schoolEntity,
                        genos.StageEnteredTick,
                        genos.EnrollmentAttendance <= 0f ? 0.5f : genos.EnrollmentAttendance,
                        genos.IsUnenrolled || schoolEntity == Entity.Null)
                    {
                        MonthsAttended = genos.EnrollmentMonths
                    });
                }

                if (genos.IsDead)
                {
                    world.Add(entity, new Dead(
                        genos.DeathTick == 0 ? restoredTick : genos.DeathTick,
                        Enum.TryParse<DeathCause>(genos.DeathCause, out var deathCause) ? deathCause : DeathCause.Other));
                }
            }

            foreach (var genos in state.Genoses)
            {
                if (!genosEntities.TryGetValue(genos.NetworkId, out var entity))
                    continue;
                if (genos.EmployerNetworkId == 0 || genos.SalaryCents <= 0)
                    continue;
                if (!companyEntities.TryGetValue(genos.EmployerNetworkId, out var company))
                    continue;
                officeEntities.TryGetValue(genos.OfficeNetworkId, out var office);

                var employee = new Employee(
                    company,
                    office,
                    genos.Position,
                    new Money(genos.SalaryCents))
                {
                    Seniority = Enum.TryParse<Seniority>(genos.Seniority, out var seniority)
                        ? seniority
                        : Seniority.Associate,
                    HiredAtTick = genos.HiredAtTick
                };
                world!.Add(entity, employee);
                world.Add(entity, new EmploymentRecord(genos.HiredAtTick)
                {
                    Attendance = genos.EmploymentAttendance,
                    Performance = genos.EmploymentPerformance,
                    TenureCycles = genos.EmploymentTenureCycles,
                    Warnings = genos.EmploymentWarnings,
                    ShiftsCompleted = genos.EmploymentShiftsCompleted,
                    ShiftsExpected = genos.EmploymentShiftsExpected,
                    Interruptions = genos.EmploymentInterruptions,
                    ConsecutiveLowMonths = genos.EmploymentConsecutiveLowMonths,
                    HighPerformanceMonths = genos.EmploymentHighPerformanceMonths,
                    LastReviewTick = genos.EmploymentLastReviewTick
                });
                if (genos.WorkOfficeNetworkId != 0 &&
                    officeEntities.TryGetValue(genos.WorkOfficeNetworkId, out var workOffice))
                {
                    world.Add(entity, new WorkSchedule(workOffice)
                    {
                        IsAtWork = genos.IsAtWork,
                        ShiftTicksRemaining = genos.ShiftTicksRemaining,
                        LastShiftStartTick = genos.LastShiftStartTick,
                        LastShiftEndTick = genos.LastShiftEndTick,
                        ShiftsCompleted = genos.WorkShiftsCompleted,
                        ShiftsInterrupted = genos.WorkShiftsInterrupted
                    });
                }
                else
                {
                    world.Add(entity, new WorkSchedule(office));
                }
                employmentIndex!.Add(company, entity);
            }

            foreach (var genos in state.Genoses)
            {
                if (!genosEntities.TryGetValue(genos.NetworkId, out var entity))
                    continue;
                if (genos.ClaimCompanyNetworkId == 0)
                    continue;
                if (!companyEntities.TryGetValue(genos.ClaimCompanyNetworkId, out var claimCompany))
                    continue;
                if (genos.UnpaidNetCents == 0 && genos.UnpaidGrossCents == 0 && genos.SeveranceCents == 0)
                    continue;

                world!.Add(entity, new WageClaim(
                    claimCompany,
                    new Money(genos.UnpaidGrossCents),
                    new Money(genos.UnpaidNetCents),
                    genos.ClaimFirstTick)
                {
                    Severance = new Money(genos.SeveranceCents),
                    CyclesInArrears = genos.ClaimCycles
                });
            }

            foreach (var genos in state.Genoses)
            {
                if (!genosEntities.TryGetValue(genos.NetworkId, out var entity))
                    continue;

                if (genos.RetirementMonthlyCents != 0)
                {
                    world!.Add(entity, new RetirementIncome(
                        new Money(genos.RetirementMonthlyCents),
                        genos.RetirementStartedTick,
                        genos.RetirementFund,
                        genos.RetirementMeansTested)
                    {
                        Reduced = genos.RetirementReduced
                    });
                }

                if (genos.UnemploymentMaxCycles > 0 || genos.UnemploymentBenefitCents != 0)
                {
                    world.Add(entity, new UnemploymentRecord(
                        genos.UnemploymentStartedTick,
                        new Money(genos.UnemploymentBenefitCents),
                        genos.UnemploymentMaxCycles,
                        genos.UnemploymentEligible)
                    {
                        CyclesClaimed = genos.UnemploymentCyclesClaimed,
                        UnpaidCycles = genos.UnemploymentUnpaidCycles
                    });
                }
            }

            foreach (var application in state.Applications)
            {
                if (!genosEntities.TryGetValue(application.ApplicantNetworkId, out var applicant))
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
                if (!genosEntities.TryGetValue(household.HeadNetworkId, out var head))
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
                if (!genosEntities.TryGetValue(member.GenosNetworkId, out var genos))
                    continue;
                if (world!.Has<HouseholdMember>(genos))
                    continue;

                world.Add(genos, new HouseholdMember(
                    household,
                    Enum.TryParse<HouseholdRole>(member.Role, out var role)
                        ? role
                        : HouseholdRole.Other));
            }

            foreach (var persisted in state.Relationships)
            {
                if (!genosEntities.TryGetValue(persisted.FromGenosNetworkId, out var from) ||
                    !genosEntities.TryGetValue(persisted.ToGenosNetworkId, out var to))
                {
                    // Legacy saves may contain edges whose endpoint was destroyed
                    // before the old estate cleanup ran. Prune them instead of
                    // blocking startup; unknown kinds/sources still fail below.
                    continue;
                }
                if (!Enum.TryParse<RelationshipKind>(persisted.Kind, out var kind))
                    throw new InvalidOperationException(
                        $"Relationship {persisted.NetworkId} has unknown kind '{persisted.Kind}'.");
                if (!Enum.TryParse<RelationshipSource>(persisted.Source, out var source))
                    throw new InvalidOperationException(
                        $"Relationship {persisted.NetworkId} has unknown source '{persisted.Source}'.");

                var sinceTick = kind == RelationshipKind.ExPartner && persisted.EndedTick is { } endedTick
                    ? endedTick
                    : persisted.SinceTick;
                var edge = world!.Create(new Relationship
                {
                    From = from,
                    To = to,
                    Kind = kind,
                    Source = source,
                    SinceTick = sinceTick
                });
                var relationshipNetworkId = new NetworkId(persisted.NetworkId);
                world.Add(edge, relationshipNetworkId);
                networkIds.AllocateWithValue(edge, relationshipNetworkId);
            }

            foreach (var persisted in state.Pregnancies)
            {
                if (!Enum.TryParse<PregnancyStatus>(persisted.Status, out var status))
                    throw new InvalidOperationException(
                        $"Pregnancy {persisted.NetworkId} has unknown status '{persisted.Status}'.");
                if (!Enum.TryParse<PregnancySource>(persisted.Source, out var source))
                    throw new InvalidOperationException(
                        $"Pregnancy {persisted.NetworkId} has unknown source '{persisted.Source}'.");

                if (!genosEntities.TryGetValue(persisted.CarryingParentNetworkId, out var carrying) ||
                    !genosEntities.TryGetValue(persisted.OtherParentNetworkId, out var other))
                {
                    // Resolved pregnancies are retained as history. A parent may
                    // legitimately have been destroyed since resolution, so drop
                    // that history instead of making the whole save unrestorable.
                    if (status is PregnancyStatus.Active or PregnancyStatus.ReadyToBirth)
                        throw new InvalidOperationException(
                            $"Active pregnancy {persisted.NetworkId} references a missing parent " +
                            $"({persisted.CarryingParentNetworkId}, {persisted.OtherParentNetworkId}).");
                    continue;
                }

                var pregnancy = world!.Create(new Pregnancy
                {
                    CarryingParent = carrying,
                    OtherParent = other,
                    ConceivedTick = persisted.ConceivedTick,
                    DueTick = persisted.DueTick,
                    RiskBasisPoints = persisted.RiskBasisPoints,
                    HealthAtConception = persisted.HealthAtConception,
                    Status = status,
                    Source = source,
                    LastProgressTick = persisted.LastProgressTick,
                    ChildrenExpected = Math.Max(1, persisted.ChildrenExpected),
                    Complications = persisted.Complications,
                    CarePaid = new Money(persisted.CarePaidCents),
                    CareArrears = new Money(persisted.CareArrearsCents),
                    CareMonths = persisted.CareMonths,
                    MissedCareMonths = persisted.MissedCareMonths,
                    ResolvedTick = persisted.ResolvedTick
                });
                var pregnancyNetworkId = new NetworkId(persisted.NetworkId);
                world.Add(pregnancy, pregnancyNetworkId);
                networkIds.AllocateWithValue(pregnancy, pregnancyNetworkId);
            }

            foreach (var persisted in state.Trusts)
            {
                if (!genosEntities.TryGetValue(persisted.BeneficiaryNetworkId, out var beneficiary))
                    throw new InvalidOperationException(
                        $"Trust {persisted.NetworkId} references a missing beneficiary " +
                        $"{persisted.BeneficiaryNetworkId}.");
                Entity guardian = Entity.Null;
                if (persisted.GuardianNetworkId != 0 &&
                    !genosEntities.TryGetValue(persisted.GuardianNetworkId, out guardian))
                    throw new InvalidOperationException(
                        $"Trust {persisted.NetworkId} references a missing guardian " +
                        $"{persisted.GuardianNetworkId}.");

                var trust = world!.Create(new TrustAccount
                {
                    Balance = new Money(persisted.BalanceCents),
                    Beneficiary = beneficiary,
                    Guardian = guardian,
                    CreatedTick = persisted.CreatedTick,
                    ReleaseAgeTicks = persisted.ReleaseAgeTicks,
                    SourceGenosNetworkId = persisted.SourceGenosNetworkId
                });
                var trustNetworkId = new NetworkId(persisted.NetworkId);
                world.Add(trust, trustNetworkId);
                networkIds.AllocateWithValue(trust, trustNetworkId);
            }

            foreach (var persisted in state.StateCare)
            {
                if (!genosEntities.TryGetValue(persisted.ChildNetworkId, out var child))
                    throw new InvalidOperationException(
                        $"State care row references a missing child {persisted.ChildNetworkId}.");
                world!.Add(child, new StateCare
                {
                    SinceTick = persisted.SinceTick,
                    CaseWorker = Entity.Null,
                    MonthlyCost = new Money(persisted.MonthlyCostCents)
                });
            }

            playerRegistry.Clear();
            foreach (var persisted in state.PlayerProfiles)
            {
                Entity genos = Entity.Null;
                if (persisted.GenosNetworkId != 0 &&
                    genosEntities.TryGetValue(persisted.GenosNetworkId, out var resolvedGenos))
                    genos = resolvedGenos;

                var profile = playerRegistry.GetOrCreate(persisted.PlayerId, genos, persisted.CreatedTick);
                profile.ContinuationPolicy = Enum.TryParse<PlayerContinuationPolicy>(
                    persisted.ContinuationPolicy, ignoreCase: true, out var policy)
                    ? policy
                    : PlayerContinuationPolicy.Respawn;
                profile.AwaitingContinuation = persisted.AwaitingContinuation || genos == Entity.Null;
                profile.Observer = persisted.Observer;
                profile.CreatedTick = persisted.CreatedTick;
                profile.LastActionTick = persisted.LastActionTick;
                profile.LastSequence = persisted.LastSequence;
                profile.LastGenosNetworkId = persisted.GenosNetworkId;
                profile.LastGenosName = persisted.LastGenosName;
                profile.LastGenosAge = persisted.LastGenosAge;
                profile.LastGenosTraits = persisted.LastGenosTraits;
                profile.LastGenosHouseholdNetworkId = persisted.LastGenosHouseholdNetworkId;
                profile.LastGenosDeathTick = persisted.LastGenosDeathTick;
                profile.PendingRespawnGenosNetworkId = persisted.PendingRespawnGenosNetworkId;

                if (genos != Entity.Null && world!.IsAlive(genos) && !world.Has<Dead>(genos) &&
                    !persisted.AwaitingContinuation && !world.Has<PlayerControlled>(genos))
                {
                    world.Add(genos, new PlayerControlled(persisted.PlayerId));
                }
            }

            socialGraph.Restore(new Genelife.Social.SocialGraphSnapshot(
                state.SocialEdges.Select(edge => new Genelife.Social.SocialEdge
                {
                    FromPlayerId = edge.FromPlayerId,
                    ToPlayerId = edge.ToPlayerId,
                    Kind = edge.Kind,
                    SinceTick = edge.SinceTick,
                    Source = edge.Source
                }).ToArray(),
                state.SocialRequests.Select(request => new Genelife.Social.SocialRequest
                {
                    Id = request.Id,
                    FromPlayerId = request.FromPlayerId,
                    ToPlayerId = request.ToPlayerId,
                    Kind = request.Kind,
                    Status = request.Status,
                    CreatedTick = request.CreatedTick,
                    ExpiresTick = request.ExpiresTick
                }).ToArray(),
                socialGraph.Reports())
            {
                Restrictions = state.SocialRestrictions.Select(restriction =>
                    new Genelife.Social.SocialRestriction(
                        restriction.ActionId,
                        restriction.PlayerId,
                        restriction.UntilTick,
                        restriction.ChatBlocked,
                        restriction.ActivityBlocked,
                        restriction.InvitationsBlocked,
                        restriction.Reason,
                        restriction.CreatedTick)).ToArray()
            });

            socialPrivacy?.Restore(state.SocialSettings.Select(setting =>
                new Genelife.Social.SocialPrivacySetting(
                    setting.PlayerId,
                    setting.ShowDisplayName,
                    setting.AllowNearbyForNonFriends,
                    setting.FriendsOnlyMessages)));

            playerRelationships?.RestoreInvitations(state.PlayerRelationshipInvitations
                .Select(invitation => new Genelife.Family.PlayerRelationshipInvitation
                {
                    Id = invitation.Id,
                    FromPlayerId = invitation.FromPlayerId,
                    ToPlayerId = invitation.ToPlayerId,
                    Kind = Enum.TryParse<Genelife.Components.Family.FamilyInvitationKind>(
                        invitation.Kind, out var kind) ? kind : Genelife.Components.Family.FamilyInvitationKind.Partnership,
                    Status = Enum.TryParse<Genelife.Components.Family.FamilyInvitationStatus>(
                        invitation.Status, out var status) ? status : Genelife.Components.Family.FamilyInvitationStatus.Cancelled,
                    CreatedTick = invitation.CreatedTick,
                    ExpiresTick = invitation.ExpiresTick
                }));

            foreach (var persisted in state.PlayerLives)
            {
                if (!genosEntities.TryGetValue(persisted.GenosNetworkId, out var genos) || !world!.IsAlive(genos))
                    continue;

                if (persisted.HasEmployment)
                {
                    Entity office = Entity.Null;
                    if (persisted.EmploymentOfficeNetworkId != 0)
                        officeEntities.TryGetValue(persisted.EmploymentOfficeNetworkId, out office);
                    if (office == Entity.Null && world.Has<Employee>(genos))
                        office = world.Get<Employee>(genos).Office;

                    world.Add(genos, new PlayerEmployment(
                        office,
                        persisted.HiredTick,
                        Enum.TryParse<PlayerEmploymentSource>(persisted.EmploymentSource, out var source)
                            ? source
                            : PlayerEmploymentSource.PlayerApplied)
                    {
                        LastAttendedTick = persisted.LastAttendedTick,
                        ConsecutiveMissedShifts = persisted.ConsecutiveMissedShifts,
                        AttendedThisMonth = persisted.AttendedThisMonth
                    });
                }

                Entity household = Entity.Null;
                if (persisted.HouseholdNetworkId != 0)
                    householdEntities.TryGetValue(persisted.HouseholdNetworkId, out household);
                Entity unit = Entity.Null;
                if (persisted.UnitNetworkId != 0)
                    unitEntities.TryGetValue(persisted.UnitNetworkId, out unit);

                if (household != Entity.Null)
                {
                    world.Add(genos, new PlayerHousing(
                        household,
                        unit,
                        persisted.LeaseStartTick,
                        new Money(persisted.DepositCents))
                    {
                        LeaseActive = persisted.LeaseActive
                    });

                    if (!world.Has<HouseholdPantry>(household))
                    {
                        world.Add(household, new HouseholdPantry
                        {
                            FoodUnits = persisted.FoodUnits,
                            HouseholdGoods = persisted.HouseholdGoods,
                            LastPurchaseTick = persisted.LastPurchaseTick
                        });
                    }
                }
                else if (!world.Has<HouseholdPantry>(genos) &&
                         (persisted.FoodUnits != 0 || persisted.HouseholdGoods != 0))
                {
                    world.Add(genos, new HouseholdPantry
                    {
                        FoodUnits = persisted.FoodUnits,
                        HouseholdGoods = persisted.HouseholdGoods,
                        LastPurchaseTick = persisted.LastPurchaseTick
                    });
                }

                if (persisted.JobOfferId != Guid.Empty &&
                    postingEntities.TryGetValue(persisted.JobOfferPostingNetworkId, out var posting))
                {
                    officeEntities.TryGetValue(persisted.JobOfferOfficeNetworkId, out var offerOffice);
                    world.Add(genos, new PlayerJobOffer
                    {
                        OfferId = persisted.JobOfferId,
                        Posting = posting,
                        Office = offerOffice,
                        Position = persisted.JobOfferPosition,
                        SalaryPerCycle = new Money(persisted.JobOfferSalaryCents),
                        CreatedTick = persisted.JobOfferCreatedTick,
                        ExpiresTick = persisted.JobOfferExpiresTick
                    });
                }

                if (persisted.LeaseOfferId != Guid.Empty &&
                    unitEntities.TryGetValue(persisted.LeaseOfferUnitNetworkId, out var offerUnit))
                {
                    propertyEntities.TryGetValue(persisted.LeaseOfferPropertyNetworkId, out var offerProperty);
                    world.Add(genos, new PlayerLeaseOffer
                    {
                        OfferId = persisted.LeaseOfferId,
                        Unit = offerUnit,
                        Property = offerProperty,
                        MonthlyRent = new Money(persisted.LeaseOfferRentCents),
                        CreatedTick = persisted.LeaseOfferCreatedTick,
                        ExpiresTick = persisted.LeaseOfferExpiresTick
                    });
                }
            }

            foreach (var persisted in state.FamilyInvitations)
            {
                if (!genosEntities.TryGetValue(persisted.FromGenosNetworkId, out var from) ||
                    !genosEntities.TryGetValue(persisted.ToGenosNetworkId, out var to))
                    continue;
                if (!Enum.TryParse<FamilyInvitationKind>(persisted.Kind, out var kind) ||
                    !Enum.TryParse<FamilyInvitationStatus>(persisted.Status, out var status))
                    continue;

                var entity = world!.Create(new FamilyInvitation(
                    persisted.Id,
                    from,
                    to,
                    kind,
                    persisted.CreatedTick,
                    persisted.ExpiresTick)
                {
                    Status = status
                });
                if (persisted.NetworkId != 0)
                {
                    var invitationId = new NetworkId(persisted.NetworkId);
                    world.Add(entity, invitationId);
                    networkIds.AllocateWithValue(entity, invitationId);
                }
                else
                {
                    world.Add(entity, networkIds.Allocate(entity));
                }
            }

            var utilityNetworkEntities = new Dictionary<ulong, Entity>();
            foreach (var persisted in state.UtilityNetworks)
            {
                var entity = world!.Create(new UtilityNetwork
                {
                    Service = Enum.TryParse<UtilityService>(persisted.Service, out var service)
                        ? service
                        : UtilityService.Water,
                    Production = Math.Max(0, persisted.Production),
                    Capacity = Math.Max(0, persisted.Capacity),
                    Load = Math.Max(0, persisted.Load),
                    Reserve = Math.Max(0, persisted.Reserve),
                    State = Enum.TryParse<UtilityNetworkState>(persisted.State, out var networkState)
                        ? networkState
                        : UtilityNetworkState.Normal
                });
                var networkId = new NetworkId(persisted.NetworkId);
                world.Add(entity, networkId);
                networkIds.AllocateWithValue(entity, networkId);
                utilityNetworkEntities[persisted.NetworkId] = entity;
            }

            foreach (var persisted in state.ServiceLinks)
            {
                Entity building = Entity.Null;
                if (!propertyEntities.TryGetValue(persisted.BuildingNetworkId, out building))
                    officeEntities.TryGetValue(persisted.BuildingNetworkId, out building);
                if (building == Entity.Null ||
                    !utilityNetworkEntities.TryGetValue(persisted.UtilityNetworkId, out var network) ||
                    !Enum.TryParse<UtilityService>(persisted.Service, out var service))
                    continue;

                var entity = world!.Create(
                    new BuildingServiceLink
                    {
                        Network = network,
                        Service = service,
                        Demand = Math.Max(0, persisted.Demand),
                        Connected = persisted.Connected,
                        Priority = persisted.Priority
                    },
                    new ServiceLinkOwner { Building = building });
                var linkId = persisted.NetworkId != 0
                    ? new NetworkId(persisted.NetworkId)
                    : networkIds.Allocate(entity);
                world.Add(entity, linkId);
                if (persisted.NetworkId != 0)
                    networkIds.AllocateWithValue(entity, linkId);
            }

            foreach (var persisted in state.HouseholdMeters)
            {
                if (!householdEntities.TryGetValue(persisted.HouseholdNetworkId, out var household))
                    continue;
                world!.Add(household, new HouseholdMeter
                {
                    WaterUsage = Math.Max(0, persisted.WaterUsage),
                    ElectricityUsage = Math.Max(0, persisted.ElectricityUsage),
                    WaterLevel = Enum.TryParse<ServiceLevel>(persisted.WaterLevel, out var waterLevel)
                        ? waterLevel
                        : ServiceLevel.Full,
                    ElectricityLevel = Enum.TryParse<ServiceLevel>(persisted.ElectricityLevel, out var electricityLevel)
                        ? electricityLevel
                        : ServiceLevel.Full,
                    Arrears = new Money(persisted.ArrearsCents),
                    LastReadingTick = persisted.LastReadingTick,
                    ConsecutivePoorMonths = Math.Max(0, persisted.ConsecutivePoorMonths),
                    LastCutOffTick = persisted.LastCutOffTick
                });
            }

            foreach (var persisted in state.StoreInventories)
            {
                if (!officeEntities.TryGetValue(persisted.StoreNetworkId, out var store))
                    continue;
                var entity = world!.Create(
                    new StoreInventory
                    {
                        ItemIndex = persisted.ItemIndex,
                        Stock = Math.Max(0, persisted.Stock),
                        CurrentPrice = new Money(persisted.CurrentPriceCents),
                        LastRestockTick = persisted.LastRestockTick
                    },
                    new StoreStockLink { Store = store });
                var inventoryId = persisted.NetworkId != 0
                    ? new NetworkId(persisted.NetworkId)
                    : networkIds.Allocate(entity);
                world.Add(entity, inventoryId);
                if (persisted.NetworkId != 0)
                    networkIds.AllocateWithValue(entity, inventoryId);
            }

            foreach (var persisted in state.Nutrition)
            {
                if (!genosEntities.TryGetValue(persisted.GenosNetworkId, out var genos))
                    continue;
                world!.Add(genos, new NutritionState
                {
                    Balance = Math.Clamp(persisted.Balance, -100, 100),
                    ConsecutivePoorDays = Math.Max(0, persisted.ConsecutivePoorDays),
                    LastMealTick = persisted.LastMealTick
                });
            }

            var consumptionBootstrap = new Genelife.Consumption.UtilityNetworkService(
                world!,
                economy!,
                entity => networkIds.AllocateInfrastructure(entity));
            consumptionBootstrap.EnsureNetworks();
            consumptionBootstrap.EnsureBuildingLinks();
            foreach (var (_, householdEntity) in householdEntities)
            {
                if (!world!.Has<HouseholdMeter>(householdEntity))
                    world.Add(householdEntity, new HouseholdMeter
                    {
                        WaterLevel = ServiceLevel.Full,
                        ElectricityLevel = ServiceLevel.Full
                    });
            }

            var groceryBootstrap = new Genelife.Consumption.GroceryStoreService(
                world!,
                economy!,
                entity => networkIds.AllocateInfrastructure(entity));
            foreach (var (_, officeEntity) in officeEntities)
                groceryBootstrap.EnsureInventory(officeEntity, tick);

            relationshipIndex.Rebuild(world!);
            relationshipCache.Clear();
            pregnancyIndex.Rebuild(world!);

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
                    row.GenosNetworkId,
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
                    row.DeathCauses.Select(cause => new DeathCauseCount(cause.Cause, cause.Count)).ToList(),
                    row.BirthsThisYear,
                    row.BirthRate,
                    row.NaturalChange,
                    row.NetMigration,
                    row.ReplacementRate,
                    row.Children,
                    row.ChildrenUnderSchoolAge,
                    row.AverageHouseholdSize,
                    row.Orphans,
                    row.StateCareChildren,
                    row.GuardianCoverage,
                    new Money(row.MinorTrustTotalCents),
                    new Money(row.InheritancePaidCents),
                    new Money(row.TrustReleasedCents)));
            }

            if (!state.LifecycleMigrated)
            {
                foreach (var genos in state.Genoses)
                {
                    if (!genosEntities.TryGetValue(genos.NetworkId, out var entity) || genos.IsDead)
                        continue;

                    var stage = economy.Life.Policy.StageFor(genos.Age);
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
                if (!genosEntities.TryGetValue(row.GenosNetworkId, out var deadGenos) ||
                    !world!.IsAlive(deadGenos))
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
                    if (genosEntities.TryGetValue(heirId, out var heir) && world.IsAlive(heir))
                        heirs.Add(heir);
                }

                estateRequests.Add(new EstateRequest
                {
                    Genos = deadGenos,
                    Household = household,
                    Unit = unit,
                    OwnedUnit = ownedUnit,
                    Tick = row.Tick,
                    Name = row.Name,
                    GenosNetworkId = row.GenosNetworkId,
                    Age = row.Age,
                    Cause = Enum.TryParse<DeathCause>(row.Cause, out var estateCause)
                        ? estateCause
                        : DeathCause.Other,
                    Heirs = heirs,
                    HouseholdRentArrears = row.HouseholdRentArrears,
                    HouseholdUtilityArrears = row.HouseholdUtilityArrears
                });
                queuedDeaths.Add(row.GenosNetworkId);
            }

            foreach (var genos in state.Genoses.Where(genos => genos.IsDead))
            {
                if (queuedDeaths.Contains(genos.NetworkId) ||
                    !genosEntities.TryGetValue(genos.NetworkId, out var deadGenos) ||
                    !world!.IsAlive(deadGenos))
                    continue;

                economy.Life.DeathQueue.Add(deadGenos);
            }

            if (economy.Life.DeathQueue.Count > 0)
                new DeathProcessingSystem(
                    world!,
                    economy,
                    relationships: relationshipService,
                    relationshipIndex: relationshipIndex,
                    guardianship: guardianshipService,
                    inheritanceOptions: inheritanceOptions,
                    trusts: trustService,
                    pregnancies: pregnancyService).Update(state.Tick);

            economy.Life.EstateQueue.AddRange(estateRequests);
            if (economy.Life.EstateQueue.Count > 0)
                new EstateSettlementSystem(
                    world!,
                    economy,
                    relationshipIndex: relationshipIndex,
                    relationshipService: relationshipService,
                    inheritanceOptions: inheritanceOptions,
                    trusts: trustService).Update(state.Tick);

            foreach (var genos in state.Genoses.Where(genos => genos.IsDead))
            {
                if (genosEntities.TryGetValue(genos.NetworkId, out var deadGenos) && world!.IsAlive(deadGenos))
                    economy.Life.QueueDestroy(deadGenos, restoredTick + 1);
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
                            "Lifecycle restore mismatch for genos " +
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
            economy.CompaniesFailedThisYear = state.Fiscal.CompaniesFailedThisYear;

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
                    snapshot.BankruptCompanies,
                    snapshot.CompanyFailureRate));
            }

            foreach (var pending in state.PendingRevenue)
            {
                var amount = new Money(pending.AmountCents);
                if (companyEntities.TryGetValue(pending.CompanyNetworkId, out var company))
                {
                    economy.BookRevenue(company, pending.Category, pending.GenosNetworkId, amount, pending.Tick);
                }
                else
                {
                    economy.Government.Balance += amount;
                }
            }

            tick = state.Tick;

            if (!state.BalanceBackfilled)
                BackfillBalanceStock(state, tick);
        }
    }

    /// <summary>
    /// One-time migration for saves created before the 2026-11 balance pass.
    /// New saves are captured with <see cref="PersistedWorldState.BalanceBackfilled"/>
    /// set, so this only runs for legacy snapshots.
    /// </summary>
    private void BackfillBalanceStock(PersistedWorldState state, long tick)
    {
        var living = economy!.Life.Index.TotalLiving;
        var pensionSeed = economy.Options.SeededPensionFundPerGenos * living;
        if (!economy.Pension.Balance.IsPositive && pensionSeed.IsPositive &&
            economy.Government.Balance >= pensionSeed)
        {
            economy.Government.Balance -= pensionSeed;
            economy.Pension.Balance += pensionSeed;
            economy.Ledger.Record(
                Guid.NewGuid(),
                tick,
                LedgerKind.PensionContribution,
                EconomyAccountIds.Government,
                EconomyAccountIds.PensionFund,
                pensionSeed,
                "restore.pension.backfill");
        }

        var companies = new List<Entity>();
        world!.Query(in new QueryDescription().WithAll<CompanyName, NetworkId>(),
            (Entity entity, ref CompanyName _, ref NetworkId _) => companies.Add(entity));
        companies.Sort((a, b) =>
        {
            var hasA = networkIds.TryGetNetworkId(a, out var idA);
            var hasB = networkIds.TryGetNetworkId(b, out var idB);
            if (hasA && hasB)
                return idA.Value.CompareTo(idB.Value);
            return a.Id.CompareTo(b.Id);
        });

        var companyOwnedProperties = 0;
        world!.Query(in new QueryDescription().WithAll<Property>(),
            (ref Property property) =>
            {
                if (property.Owner != Entity.Null)
                    companyOwnedProperties++;
            });

        if (companyOwnedProperties == 0 && companies.Count > 0)
        {
            var basicProperties = new List<Entity>();
            world!.Query(in new QueryDescription().WithAll<Property>(),
                (Entity entity, ref Property property) =>
                {
                    if (property.Owner == Entity.Null && !property.IsShelter &&
                        property.Name.Contains("Basic", StringComparison.OrdinalIgnoreCase))
                        basicProperties.Add(entity);
                });

            for (var i = 0; i < basicProperties.Count; i++)
            {
                var owner = companies[i % companies.Count];
                economy.Housing.SetPropertyOwner(basicProperties[i], owner);
                var data = world!.Get<Property>(basicProperties[i]);
                data.Owner = owner;
                world.Get<Property>(basicProperties[i]) = data;
            }
        }

        // Legacy saves keep the old 2-company stock. Run one growth year now;
        // subsequent years are handled by the registered JobGrowthSystem.
        new JobGrowthSystem(world!, networkIds, economy, employmentIndex!, mapContext).Update(tick);

        state.BalanceBackfilled = true;
    }
}