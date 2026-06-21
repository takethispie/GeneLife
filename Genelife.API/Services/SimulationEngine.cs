using Arch.Core;
using Genelife.Systems;

namespace Genelife.Api.Services;

public class SimulationEngine : IDisposable
{
    private World? world;
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

    public void Start()
    {
        lock (@lock)
        {
            if (isRunning)
                return;

            world = World.Create();
            needsDecaySystem = new NeedsDecaySystem(world);
            decisionSystem = new DecisionSystem(world);
            actionSystem = new ActionSystem(world);
            hiringSystem = new HiringSystem(world);
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
            world?.Dispose();
            world = null;
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

            needsDecaySystem?.Update(DeltaTime);
            decisionSystem?.Update();
            actionSystem?.Update(DeltaTime);
            hiringSystem?.Update();
            payrollSystem?.Update();
            jobSeekerSystem?.Update();
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
