using System.Diagnostics;
using Arch.Core;
using Genelife.Api.DTOs;
using Genelife.Api.Snapshots;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
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

    private static List<SimStatus> BuildSimSnapshots(World world)
    {
        var sims = new List<SimStatus>();
        var query = new QueryDescription().WithAll<GenName, Needs, CurrentAction, NetworkId>();

        world.Query(in query, (ref GenName name, ref Needs needs, ref CurrentAction action, ref NetworkId networkId) =>
        {
            sims.Add(new SimStatus
            {
                NetworkId = networkId.Value,
                Name = name.Name,
                Hunger = needs.Hunger,
                Energy = needs.Energy,
                Hygiene = needs.Hygiene,
                Bladder = needs.Bladder,
                CurrentAction = action.Type.ToString(),
                TimeRemaining = action.TimeRemaining
            });
        });

        return sims;
    }

    private static List<CompanyStatus> BuildCompanySnapshots(World world, EmploymentIndex index)
    {
        var companies = new List<CompanyStatus>();
        var query = new QueryDescription().WithAll<CompanyName, NetworkId>();

        world.Query(in query, (Entity entity, ref CompanyName companyName, ref NetworkId networkId) =>
        {
            companies.Add(new CompanyStatus
            {
                NetworkId = networkId.Value,
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

    public void Dispose()
    {
        Shutdown();
    }
}