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
    private EmploymentIndex? employmentIndex;
    private NeedsDecaySystem? needsDecaySystem;
    private DecisionSystem? decisionSystem;
    private ActionSystem? actionSystem;
    private HiringSystem? hiringSystem;
    private PayrollSystem? payrollSystem;
    private JobSeekerSystem? jobSeekerSystem;
    private Timer? timer;
    private bool isRunning;
    private long tick;
#pragma warning disable IDE0044
    private WorldSnapshot? _next;
#pragma warning restore IDE0044
    private WorldSnapshot? _latest;
    private readonly object @lock = new();
    private const float DeltaTime = 1f;

    public bool IsRunning { get { lock (@lock) { return isRunning; } } }
    public World? World { get { lock (@lock) { return world; } } }
    public EmploymentIndex? EmploymentIndex { get { lock (@lock) { return employmentIndex; } } }

    public long LastSnapshotBuildTimeMs { get; private set; }

    public void Start()
    {
        lock (@lock)
        {
            if (isRunning) return;
            world = World.Create();
            employmentIndex = new EmploymentIndex();
            needsDecaySystem = new NeedsDecaySystem(world);
            decisionSystem = new DecisionSystem(world);
            actionSystem = new ActionSystem(world);
            hiringSystem = new HiringSystem(world, employmentIndex);
            payrollSystem = new PayrollSystem(world);
            jobSeekerSystem = new JobSeekerSystem(world);
            timer = new Timer(Tick, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(40));
            tick = 0;
            isRunning = true;
        }
    }

    public void Stop()
    {
        lock (@lock)
        {
            if (!isRunning) return;
            timer?.Dispose();
            timer = null;
            world?.Dispose();
            world = null;
            employmentIndex = null;
            _next = null;
            _latest = null;
            isRunning = false;
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

    public WorldSnapshot? GetLatestSnapshot()
    {
        return Volatile.Read(ref _latest);
    }

    private void Tick(object? state)
    {
        lock (@lock)
        {
            if (!isRunning || world == null) return;

            tick++;

            needsDecaySystem?.Update(DeltaTime);
            decisionSystem?.Update();
            actionSystem?.Update(DeltaTime);
            hiringSystem?.Update();
            payrollSystem?.Update();
            jobSeekerSystem?.Update();

            var sw = Stopwatch.StartNew();

            var sims = BuildSimSnapshots(world);
            var companies = BuildCompanySnapshots(world, employmentIndex!);

            _next = new WorldSnapshot(tick, WorldSnapshot.CurrentVersion, sims, companies);

            sw.Stop();
            LastSnapshotBuildTimeMs = sw.ElapsedMilliseconds;

            Interlocked.Exchange(ref _latest, _next);
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
        var query = new QueryDescription().WithAll<CompanyName>();

        world.Query(in query, (Entity entity, ref CompanyName companyName) =>
        {
            companies.Add(new CompanyStatus
            {
                EntityId = entity.Id,
                Name = companyName.Name,
                EmployeeCount = index.CountEmployees(entity)
            });
        });

        return companies;
    }

    public void Dispose() => Stop();
}