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
using Genelife.Economy;
using Genelife.Enums;
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

    public SimulationEngine(WorldSaveService? worldSaveService = null, IDocumentStore? documentStore = null)
    {
        this.worldSaveService = worldSaveService;
        this.documentStore = documentStore;
        recordSystemRun = result =>
            metrics.RecordSystem(result.Name, result.Milliseconds, result.AllocatedBytes);
    }

    public bool IsRunning { get { lock (@lock) { return isRunning; } } }
    public World? World { get { lock (@lock) { return world; } } }
    public EconomyContext? Economy { get { lock (@lock) { return economy; } } }
    public SimulationClock Clock => clock;
    public SimulationClockOptions ClockOptions { get; set; } = new();
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

    public long DroppedEventCount => Interlocked.Read(ref droppedEventCount);

    public void Initialize()
    {
        lock (@lock)
        {
            if (isRunning) return;
            world = World.Create();
            metrics = new Metrics();
            employmentIndex = new EmploymentIndex();
            clock = new SimulationClock(ClockOptions);
            economy = new EconomyContext(world, employmentIndex, clock: clock, randomSeed: RandomSeed);

            systems = new SystemRegistry();
            systems.Register(new NeedsDecaySystem(world));
            systems.Register(new DecisionSystem(world));
            systems.Register(new ActionSystem(world));
            systems.Register(new HiringSystem(world, employmentIndex, 25, economy));
            systems.Register(new CompanyRevenueSystem(world, economy));
            systems.Register(new ConsumerSpendingSystem(world, economy));
            systems.Register(new PayrollSystem(world, economy));
            systems.Register(new CompanyCostsSystem(world, economy));
            systems.Register(new BasicIncomeSystem(world, economy));
            systems.Register(new BankingSystem(world, economy));
            systems.Register(new CorporateTaxSystem(world, economy));
            systems.Register(new CreditSystem(world, economy));
            systems.Register(new CompanySolvencySystem(world, economy));
            systems.Register(new JobSeekerSystem(world));
            systems.Register(new EconomyMetricsSystem(world, economy));

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
            case CreateOfficeCommand create:
            {
                var repository = new OfficeRepository(world, networkIds, economy);
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
                var repository = new OfficeRepository(world, networkIds, economy);
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
                var repository = new OfficeRepository(world, networkIds, economy);
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
                var repository = new CompanyRepository(world, employmentIndex!, networkIds, economy);
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
                    CapturedAt = DateTimeOffset.UtcNow
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
            Sims = sims
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

            return new PersistedWorldState
            {
                SchemaVersion = PersistedWorldState.CurrentSchemaVersion,
                Tick = currentTick,
                RandomSeed = economy.Random.Seed,
                Sims = sims,
                Companies = companies,
                Offices = offices,
                Postings = postings,
                Applications = applications,
                Loans = loans,
                PendingRevenue = pendingRevenue,
                MetricHistory = metricHistory,
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

                if (sim.IsLookingForWork)
                    world.Add(entity, new JobSeeker(true, Skill.None, Money.FromUnits(1000m)));

                if (sim.PlayerId != null)
                    world.Add(entity, new PlayerControlled(sim.PlayerId));
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

                world!.Add(entity, new Employee(
                    company,
                    office,
                    sim.Position,
                    new Money(sim.SalaryCents)));
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