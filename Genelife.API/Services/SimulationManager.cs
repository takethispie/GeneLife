using Arch.Core;
using Genelife.Api.DTOs;
using Genelife.Components;
using Genelife.Enums;
using Genelife.Systems;

namespace Genelife.Api.Services;

public class SimulationManager : IHostedService, IDisposable
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
            timer = new Timer(SimulationTick, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(40));
            isRunning = true;
        }
    }

    public void StopSimulation()
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

    public void AddSim(string name, int age)
    {
        lock (@lock)
        {
            if (world == null)
                throw new InvalidOperationException("Simulation is not running");

            world.Create(
                new SimName(name),
                new Needs(),
                new CurrentAction(ActionType.Idle, 0f),
                new Alive(age)
            );
        }
    }

    public List<SimStatus> GetAllSims()
    {
        lock (@lock)
        {
            if (world == null)
                return new List<SimStatus>();

            var sims = new List<SimStatus>();
            var query = new QueryDescription().WithAll<SimName, Needs, CurrentAction>();

            world.Query(in query, (ref SimName name, ref Needs needs, ref CurrentAction action) =>
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
        StopSimulation();
    }
}