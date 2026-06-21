using Arch.Core;
using Genelife.Systems;

namespace Genelife.Api.Services;

public class SimulationEngine : IDisposable
{
    private World? _world;
    private NeedsDecaySystem? _needsDecaySystem;
    private DecisionSystem? _decisionSystem;
    private ActionSystem? _actionSystem;
    private HiringSystem? _hiringSystem;
    private PayrollSystem? _payrollSystem;
    private JobSeekerSystem? _jobSeekerSystem;
    private Timer? _timer;
    private bool _isRunning;
    private readonly object _lock = new();
    private const float DeltaTime = 1f;

    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                return _isRunning;
            }
        }
    }

    public World? World
    {
        get
        {
            lock (_lock)
            {
                return _world;
            }
        }
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_isRunning)
                return;

            _world = World.Create();
            _needsDecaySystem = new NeedsDecaySystem(_world);
            _decisionSystem = new DecisionSystem(_world);
            _actionSystem = new ActionSystem(_world);
            _hiringSystem = new HiringSystem(_world);
            _payrollSystem = new PayrollSystem(_world);
            _jobSeekerSystem = new JobSeekerSystem(_world);
            _timer = new Timer(Tick, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(40));
            _isRunning = true;
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning)
                return;

            _timer?.Dispose();
            _timer = null;
            _world?.Dispose();
            _world = null;
            _isRunning = false;
        }
    }

    public T ExecuteWithWorld<T>(Func<World, T> operation)
    {
        lock (_lock)
        {
            if (_world == null)
                throw new InvalidOperationException("Simulation is not running");

            return operation(_world);
        }
    }

    private void Tick(object? state)
    {
        lock (_lock)
        {
            if (!_isRunning || _world == null)
                return;

            _needsDecaySystem?.Update(DeltaTime);
            _decisionSystem?.Update();
            _actionSystem?.Update(DeltaTime);
            _hiringSystem?.Update();
            _payrollSystem?.Update();
            _jobSeekerSystem?.Update();
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
