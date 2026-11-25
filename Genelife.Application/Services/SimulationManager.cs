using Genelife.Api.DTOs;
using Genelife.Api.Persistence;
using Genelife.Api.Repositories;
using Genelife.Components.Gen;
using Genelife.Messages.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Genelife.Api.Services;

public class SimulationManager : IHostedService, IDisposable
{
    private readonly SimulationEngine engine;
    private readonly ISnapshotStore store;
    private readonly IReadModelStore? readModelStore;
    private readonly IWorldPersistence? worldPersistence;
    private readonly ILogger<SimulationManager>? logger;
    private PeriodicTimer? saveTimer;
    private CancellationTokenSource? saveCts;

    public SimulationManager(SimulationEngine engine, ISnapshotStore store, IReadModelStore? readModelStore = null, IWorldPersistence? worldPersistence = null, ILogger<SimulationManager>? logger = null)
    {
        this.engine = engine;
        this.store = store;
        this.readModelStore = readModelStore;
        this.worldPersistence = worldPersistence;
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
            logger?.LogError(ex, "Failed to restore from snapshot; resetting simulation and starting fresh");
            engine.Shutdown();
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

    public ulong AddSim(string name, int age)
    {
        var result = ExecuteAdminSync(
            new Genelife.Api.Commands.CreateSimCommand(Guid.NewGuid(), "system", name, age));

        if (!result.Accepted || result.Payload is not ulong networkId)
            throw new InvalidOperationException(result.Error ?? "Failed to add sim.");

        return networkId;
    }

    public async Task<List<SimStatus>> GetAllSimsAsync()
    {
        if (readModelStore != null)
        {
            var sims = await readModelStore.TryGetAllSimsAsync();
            if (sims != null)
                return sims.ToList();
        }

        var snapshot = engine.GetLatestSnapshot();
        if (snapshot != null)
            return snapshot.Sims;

        return engine.ExecuteWithWorld(world =>
        {
            var repository = new SimRepository(world, networkIds, engine.Economy, engine.MapContext);
            return repository.GetAll();
        });
    }

    public ulong AddCompany(string name)
    {
        var result = ExecuteAdminSync(
            new Genelife.Api.Commands.CreateCompanyCommand(Guid.NewGuid(), "system", name));

        if (!result.Accepted || result.Payload is not ulong networkId)
            throw new InvalidOperationException(result.Error ?? "Failed to add company.");

        return networkId;
    }

    public async Task<CompanyStatus?> GetCompanyAsync(ulong networkId)
    {
        if (readModelStore != null)
        {
            var company = await readModelStore.TryGetCompanyAsync(networkId);
            if (company != null)
                return company;
        }

        var snapshot = engine.GetLatestSnapshot();
        if (snapshot != null)
        {
            var company = snapshot.Companies.Find(c => c.NetworkId == networkId);
            if (company != null)
                return company;
        }

        return engine.ExecuteWithWorld(world =>
        {
            var repository = new CompanyRepository(world, GetIndex(), networkIds, engine.Economy, map: engine.MapContext);
            return repository.Get(new NetworkId(networkId));
        });
    }

    public WorldDataStatus GetWorldData()
    {
        return engine.GetWorldData();
    }

    public MapStatus GetMapStatus() => engine.BuildMapStatus();

    public MapSnapshot GetMapSnapshot() => engine.BuildMapSnapshot();

    public MapHealthStatus GetMapHealth() => engine.BuildMapHealth();

    public IReadOnlyList<Genelife.Systems.MapMetricsSnapshot> GetMapMetrics() => engine.MapMetricsHistory;

    public MapViewportStatus? GetMapViewport(int minX, int minY, int maxX, int maxY) =>
        engine.BuildMapViewport(minX, minY, maxX, maxY);

    public MapBuildingStatus? GetMapBuilding(ulong networkId) => engine.BuildMapBuilding(networkId);

    public async Task<List<CompanyStatus>> GetAllCompaniesAsync()
    {
        if (readModelStore != null)
        {
            var companies = await readModelStore.TryGetAllCompaniesAsync();
            if (companies != null)
                return companies.ToList();
        }

        var snapshot = engine.GetLatestSnapshot();
        if (snapshot != null)
            return snapshot.Companies;

        return engine.ExecuteWithWorld(world =>
        {
            var repository = new CompanyRepository(world, GetIndex(), networkIds, engine.Economy, map: engine.MapContext);
            return repository.GetAll();
        });
    }

    public bool DeleteCompany(ulong networkId)
    {
        return engine.ExecuteWithWorld(world =>
        {
            var repository = new CompanyRepository(world, GetIndex(), networkIds, engine.Economy, map: engine.MapContext);
            return repository.Delete(new NetworkId(networkId));
        });
    }

    public bool DeleteSim(ulong networkId)
    {
        var result = ExecuteAdminSync(
            new Genelife.Api.Commands.DeleteSimCommand(Guid.NewGuid(), "system", networkId));

        if (!result.Accepted)
            throw new InvalidOperationException(result.Error ?? "Failed to delete sim.");

        return result.Payload is bool deleted && deleted;
    }

    public void SetSimAction(ulong networkId, string actionType)
    {
        var result = ExecuteAdminSync(
            new Genelife.Api.Commands.SetSimActionCommand(Guid.NewGuid(), "system", networkId, actionType));

        if (!result.Accepted)
            throw new InvalidOperationException(result.Error ?? "Failed to set sim action.");
    }

    private Genelife.Api.Commands.CommandResult ExecuteAdminSync(Genelife.Api.Commands.AdminCommand command)
    {
        try
        {
            return engine.ExecuteAdminCommandSync(command);
        }
        catch (TimeoutException ex)
        {
            throw new InvalidOperationException(
                "The simulation loop did not apply the admin command in time.",
                ex);
        }
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

            await SaveWorldDocumentsAsync();
        }
    }

    private async Task SaveWorldDocumentsAsync()
    {
        if (worldPersistence == null)
            return;

        try
        {
            var batch = engine.PrepareSave();
            if (batch != null)
                await worldPersistence.SaveBatchAsync(batch);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Periodic world save to Marten failed");
        }
    }

    public void Dispose()
    {
        StopPeriodicSave();
        (store as IDisposable)?.Dispose();
        engine.Dispose();
    }
}