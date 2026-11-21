using System.Diagnostics;
using Arch.Core;
using Genelife.Components.Employment;
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
    private Timer? timer;
    private bool isRunning;
    private readonly object @lock = new();
    private const float DeltaTime = 1f;

    public bool IsRunning
    {
        get
        {
            lock (@lock)
            {
                return isRunning;
            }
        }
    }

    public World? World
    {
        get
        {
            lock (@lock)
            {
                return world;
            }
        }
    }

    public EmploymentIndex? EmploymentIndex
    {
        get
        {
            lock (@lock)
            {
                return employmentIndex;
            }
        }
    }

    public void Start()
    {
        lock (@lock)
        {
            if (isRunning)
                return;

            world = World.Create();
            metrics = new Metrics();
            employmentIndex = new EmploymentIndex();
            needsDecaySystem = new NeedsDecaySystem(world);
            decisionSystem = new DecisionSystem(world);
            actionSystem = new ActionSystem(world);
            hiringSystem = new HiringSystem(world, employmentIndex);
            payrollSystem = new PayrollSystem(world);
            jobSeekerSystem = new JobSeekerSystem(world);
            timer = new Timer(Tick, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(40));
            isRunning = true;
        }
    }

    public void Stop()
    {
        lock (@lock)
        {
            if (!isRunning)
                return;

            timer?.Dispose();
            timer = null;
            metrics.Flush();
            world?.Dispose();
            world = null;
            employmentIndex = null;
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

    private void Tick(object? state)
    {
        lock (@lock)
        {
            if (!isRunning || world == null)
                return;

            var currentWorld = world;
            var tickStart = Stopwatch.GetTimestamp();
            var allocatedStart = GC.GetAllocatedBytesForCurrentThread();

            var systemStart = Stopwatch.GetTimestamp();
            needsDecaySystem?.Update(DeltaTime);
            metrics.RecordSystem("NeedsDecay", ElapsedMilliseconds(systemStart));

            systemStart = Stopwatch.GetTimestamp();
            decisionSystem?.Update();
            metrics.RecordSystem("Decision", ElapsedMilliseconds(systemStart));

            systemStart = Stopwatch.GetTimestamp();
            actionSystem?.Update(DeltaTime);
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

            var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedStart;
            metrics.RecordTick(ElapsedMilliseconds(tickStart), allocatedBytes, currentWorld.Size);
        }
    }

    private static double ElapsedMilliseconds(long startTimestamp)
    {
        return Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
    }

    public void Dispose()
    {
        Stop();
    }
}
