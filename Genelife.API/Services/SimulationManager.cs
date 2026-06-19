using Arch.Core;
using Genelife.Components;
using Genelife.Enums;
using Genelife.Systems;

namespace Genelife.Api.Services;

public class SimulationManager : IHostedService, IDisposable
{
    private World? _world;
    private NeedsDecaySystem? _needsDecaySystem;
    private DecisionSystem? _decisionSystem;
    private ActionSystem? _actionSystem;
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

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        StopSimulation();
        return Task.CompletedTask;
    }

    public void StartSimulation()
    {
        lock (_lock)
        {
            if (_isRunning)
                return;

            _world = World.Create();
            _needsDecaySystem = new NeedsDecaySystem(_world);
            _decisionSystem = new DecisionSystem(_world);
            _actionSystem = new ActionSystem(_world);
            _timer = new Timer(SimulationTick, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(40));
            _isRunning = true;
        }
    }

    public void StopSimulation()
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

    public void AddSim(string name)
    {
        lock (_lock)
        {
            if (_world == null)
                throw new InvalidOperationException("Simulation is not running");

            _world.Create(
                new SimName(name),
                new Needs(),
                new CurrentAction(ActionType.Idle, 0f)
            );
        }
    }

    public List<SimStatus> GetAllSims()
    {
        lock (_lock)
        {
            if (_world == null)
                return new List<SimStatus>();

            var sims = new List<SimStatus>();
            var query = new QueryDescription().WithAll<SimName, Needs, CurrentAction>();

            _world.Query(in query, (ref SimName name, ref Needs needs, ref CurrentAction action) =>
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
    }

    private void SimulationTick(object? state)
    {
        lock (_lock)
        {
            if (!_isRunning || _world == null)
                return;

            _needsDecaySystem?.Update(DeltaTime);
            _decisionSystem?.Update();
            _actionSystem?.Update(DeltaTime);
        }
    }

    public void Dispose()
    {
        StopSimulation();
    }
}

public class SimStatus
{
    public string Name { get; set; } = string.Empty;
    public float Hunger { get; set; }
    public float Energy { get; set; }
    public float Hygiene { get; set; }
    public float Bladder { get; set; }
    public string CurrentAction { get; set; } = string.Empty;
    public float TimeRemaining { get; set; }
}
