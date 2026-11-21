using Genelife.Api.DTOs;
using Genelife.Api.Persistence;
using Genelife.Api.Repositories;
using Genelife.Components.Gen;

namespace Genelife.Api.Services;

public class SimulationManager : IHostedService, IDisposable
{
    private readonly SimulationEngine engine;
    private readonly ISnapshotStore store;
    private readonly ILogger<SimulationManager>? logger;
    private PeriodicTimer? saveTimer;
    private CancellationTokenSource? saveCts;

    public SimulationManager(SimulationEngine engine, ISnapshotStore store, ILogger<SimulationManager>? logger = null)
    {
        this.engine = engine;
        this.store = store;
        this.logger = logger;
    }

    private NetworkIdRegistry networkIds => engine.NetworkIds;

    public bool IsRunning => engine.IsRunning;
    public DateTime? LastSaveTime { get; private set; }
    public DateTime? LastLoadTime { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await store.InitializeAsync(cancellationToken);

        try
        {
            var latest = store.LoadLatest();
            if (latest != null)
            {
                logger?.LogInformation("Restoring simulation from snapshot at tick {Tick}", latest.Tick);
                engine.Restore(latest, networkIds);
                LastLoadTime = DateTime.UtcNow;
                StartPeriodicSave();
            }
            else
            {
                logger?.LogInformation("No snapshot found; simulation will start fresh when requested");
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to restore from snapshot; starting fresh");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        StopPeriodicSave();

        if (engine.IsRunning)
        {
            try
            {
                SaveSnapshot();
                logger?.LogInformation("Final snapshot saved before shutdown");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to save final snapshot during shutdown");
            }
            engine.Shutdown();
            networkIds.Clear();
        }
        
        await Task.CompletedTask;
    }

    public void StartSimulation()
    {
        if (engine.IsRunning)
            return;

        networkIds.Clear();
        engine.Initialize();
        StartPeriodicSave();
    }

    public void StopSimulation()
    {
        if (!engine.IsRunning)
            return;

        StopPeriodicSave();
        engine.Shutdown();
        networkIds.Clear();
    }

    public PersistedWorldState SaveSnapshot()
    {
        if (!engine.IsRunning)
            throw new InvalidOperationException("Simulation is not running");

        var state = engine.CaptureState(networkIds);
        store.Save(state);
        LastSaveTime = DateTime.UtcNow;
        logger?.LogDebug("Snapshot saved at tick {Tick} with {SimCount} sims, {CompanyCount} companies",
            state.Tick, state.Sims.Count, state.Companies.Count);
        return state;
    }

    public void RestoreSnapshot()
    {
        if (engine.IsRunning)
            throw new InvalidOperationException("Simulation is running. Stop it before restoring.");

        var latest = store.LoadLatest();
        if (latest == null)
            throw new InvalidOperationException("No snapshot found to restore");

        networkIds.Clear();
        engine.Restore(latest, networkIds);
        LastLoadTime = DateTime.UtcNow;
        StartPeriodicSave();
    }

    public void AddSim(string name, int age)
    {
        engine.ExecuteWithWorld(world =>
        {
            var repository = new SimRepository(world, networkIds);
            repository.Add(name, age);
            return true;
        });
    }

    public List<SimStatus> GetAllSims()
    {
        var snapshot = engine.GetLatestSnapshot();
        if (snapshot != null)
            return snapshot.Sims;

        return engine.ExecuteWithWorld(world =>
        {
            var repository = new SimRepository(world, networkIds);
            return repository.GetAll();
        });
    }

    public ulong AddCompany(string name)
    {
        return engine.ExecuteWithWorld(world =>
        {
            var repository = new CompanyRepository(world, GetIndex(), networkIds);
            return repository.Add(name).Value;
        });
    }

    public CompanyStatus? GetCompany(ulong networkId)
    {
        var snapshot = engine.GetLatestSnapshot();
        if (snapshot != null)
            return snapshot.Companies.Find(c => c.NetworkId == networkId);

        return engine.ExecuteWithWorld(world =>
        {
            var repository = new CompanyRepository(world, GetIndex(), networkIds);
            return repository.Get(new NetworkId(networkId));
        });
    }

    public List<CompanyStatus> GetAllCompanies()
    {
        var snapshot = engine.GetLatestSnapshot();
        if (snapshot != null)
            return snapshot.Companies;

        return engine.ExecuteWithWorld(world =>
        {
            var repository = new CompanyRepository(world, GetIndex(), networkIds);
            return repository.GetAll();
        });
    }

    public bool DeleteCompany(ulong networkId)
    {
        return engine.ExecuteWithWorld(world =>
        {
            var repository = new CompanyRepository(world, GetIndex(), networkIds);
            return repository.Delete(new NetworkId(networkId));
        });
    }

    private Genelife.Components.Employment.EmploymentIndex GetIndex()
    {
        return engine.EmploymentIndex
            ?? throw new InvalidOperationException("Simulation is not running");
    }

    private void StartPeriodicSave()
    {
        saveCts = new CancellationTokenSource();
        saveTimer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        _ = RunPeriodicSave(saveCts.Token);
    }

    private void StopPeriodicSave()
    {
        saveCts?.Cancel();
        saveCts?.Dispose();
        saveCts = null;
        saveTimer?.Dispose();
        saveTimer = null;
    }

    private async Task RunPeriodicSave(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await saveTimer!.WaitForNextTickAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!engine.IsRunning)
                break;

            try
            {
                SaveSnapshot();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Periodic snapshot save failed");
            }
        }
    }

    public void Dispose()
    {
        StopPeriodicSave();
        (store as IDisposable)?.Dispose();
        engine.Dispose();
    }
}