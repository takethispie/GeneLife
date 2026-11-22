using Genelife.Api.DTOs;
using Genelife.Api.Models;
using Genelife.Api.Persistence;
using Genelife.Api.Repositories;
using Genelife.Components.Gen;
using Genelife.Messages.Events;
using Marten;

namespace Genelife.Api.Services;

public class SimulationManager : IHostedService, IDisposable
{
    private readonly SimulationEngine engine;
    private readonly ISnapshotStore store;
    private readonly IDocumentStore? documentStore;
    private readonly WorldSaveService? worldSaveService;
    private readonly ILogger<SimulationManager>? logger;
    private PeriodicTimer? saveTimer;
    private CancellationTokenSource? saveCts;

    public SimulationManager(SimulationEngine engine, ISnapshotStore store, IDocumentStore? documentStore = null, WorldSaveService? worldSaveService = null, ILogger<SimulationManager>? logger = null)
    {
        this.engine = engine;
        this.store = store;
        this.documentStore = documentStore;
        this.worldSaveService = worldSaveService;
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
        var networkId = engine.ExecuteWithWorld(world =>
        {
            var repository = new SimRepository(world, networkIds, engine.Economy, engine.MapContext);
            return repository.Add(name, age).Value;
        });
        engine.Emit(new SimCreated(networkId, name, age));
        return networkId;
    }

    public async Task<List<SimStatus>> GetAllSimsAsync()
    {
        if (documentStore != null)
        {
            try
            {
                await using var session = documentStore.LightweightSession();
                var docs = await session.Query<SimDocument>().ToListAsync();
                return docs
                    .GroupBy(d => d.NetworkId)
                    .Select(group => group.MaxBy(d => d.UpdatedAt)!)
                    .Select(d => d.ToSimStatus())
                    .OrderBy(sim => sim.NetworkId)
                    .ToList();
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Marten query for GetAllSims failed; falling back to snapshot");
            }
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
        var networkId = engine.ExecuteWithWorld(world =>
        {
            var repository = new CompanyRepository(world, GetIndex(), networkIds, engine.Economy, map: engine.MapContext);
            return repository.Add(name).Value;
        });
        engine.Emit(new CompanyCreated(networkId, name));
        return networkId;
    }

    public async Task<CompanyStatus?> GetCompanyAsync(ulong networkId)
    {
        if (documentStore != null)
        {
            try
            {
                await using var session = documentStore.LightweightSession();
                var doc = await session.Query<CompanyDocument>()
                    .OrderByDescending(d => d.UpdatedAt)
                    .FirstOrDefaultAsync(d => d.NetworkId == (long)networkId);
                if (doc != null)
                    return doc.ToCompanyStatus();
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Marten query for GetCompany failed; falling back to snapshot");
            }
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

    public MapHealthStatus GetMapHealth() => engine.BuildMapHealth();

    public IReadOnlyList<Genelife.Systems.MapMetricsSnapshot> GetMapMetrics() => engine.MapMetricsHistory;

    public MapViewportStatus? GetMapViewport(int minX, int minY, int maxX, int maxY) =>
        engine.BuildMapViewport(minX, minY, maxX, maxY);

    public MapBuildingStatus? GetMapBuilding(ulong networkId) => engine.BuildMapBuilding(networkId);

    public async Task<List<CompanyStatus>> GetAllCompaniesAsync()
    {
        if (documentStore != null)
        {
            try
            {
                await using var session = documentStore.LightweightSession();
                var docs = await session.Query<CompanyDocument>().ToListAsync();
                return docs
                    .GroupBy(d => d.NetworkId)
                    .Select(group => group.MaxBy(d => d.UpdatedAt)!)
                    .Select(d => d.ToCompanyStatus())
                    .OrderBy(company => company.NetworkId)
                    .ToList();
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Marten query for GetAllCompanies failed; falling back to snapshot");
            }
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
        return engine.ExecuteWithWorld(world =>
        {
            var repository = new SimRepository(world, networkIds, engine.Economy, engine.MapContext);
            return repository.Delete(networkId);
        });
    }

    public void SetSimAction(ulong networkId, string actionType)
    {
        engine.ExecuteWithWorld(world =>
        {
            var repository = new SimRepository(world, networkIds, engine.Economy, engine.MapContext);
            repository.SetAction(networkId, actionType);
            return true;
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

            await SaveWorldDocumentsAsync();
        }
    }

    private async Task SaveWorldDocumentsAsync()
    {
        if (worldSaveService == null)
            return;

        try
        {
            var batch = engine.PrepareSave();
            if (batch != null)
                await worldSaveService.SaveBatchAsync(batch);
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