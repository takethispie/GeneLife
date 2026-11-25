using Genelife.Api.DTOs;
using Genelife.Api.Persistence;
using Genelife.Api.Repositories;
using Genelife.Components.Gen;
using Genelife.Messages.Events;
using Genelife.Map;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Genelife.Api.Services;

public class SimulationManager : IHostedService, IDisposable
{
    private readonly SimulationEngine engine;
    private readonly ISnapshotStore store;
    private readonly IReadModelStore? readModelStore;
    private readonly IWorldPersistence? worldPersistence;
    private readonly IWorldResetNotifier? worldResetNotifier;
    private readonly ILogger<SimulationManager>? logger;
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private PeriodicTimer? saveTimer;
    private CancellationTokenSource? saveCts;

    public SimulationManager(SimulationEngine engine, ISnapshotStore store, IReadModelStore? readModelStore = null, IWorldPersistence? worldPersistence = null, ILogger<SimulationManager>? logger = null, IWorldResetNotifier? worldResetNotifier = null)
    {
        this.engine = engine;
        this.store = store;
        this.readModelStore = readModelStore;
        this.worldPersistence = worldPersistence;
        this.logger = logger;
        this.worldResetNotifier = worldResetNotifier;
    }

    private NetworkIdRegistry networkIds => engine.NetworkIds;

    public bool IsRunning => engine.IsRunning;
    public bool HasMap => engine.MapContext is not null;
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
        lifecycleGate.Wait();
        try
        {
            if (engine.IsRunning)
                return;

            networkIds.Clear();
            engine.Initialize();
            StartPeriodicSave();
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    public void StopSimulation()
    {
        lifecycleGate.Wait();
        try
        {
            if (!engine.IsRunning)
                return;

            StopPeriodicSave();
            engine.Shutdown();
            networkIds.Clear();
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    public PersistedWorldState SaveSnapshot()
    {
        if (!engine.IsRunning)
            throw new InvalidOperationException("Simulation is not running");

        var state = engine.CaptureState(networkIds);
        store.Save(state);
        LastSaveTime = DateTime.UtcNow;
        logger?.LogDebug("Snapshot saved at tick {Tick} with {GenosCount} genoses, {CompanyCount} companies",
            state.Tick, state.Genoses.Count, state.Companies.Count);
        return state;
    }

    public void RestoreSnapshot()
    {
        lifecycleGate.Wait();
        try
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
        finally
        {
            lifecycleGate.Release();
        }
    }

    /// <summary>
    /// Replaces the world with a freshly generated map and population. This is a
    /// destructive lifecycle operation: it stops the current simulation, builds
    /// the new world outside the tick lock, persists it and signals clients.
    /// </summary>
    public async Task<CreateMapResult> CreateMap(
        CreateMapRequest request,
        IReadOnlyCollection<string> connectedPlayerIds,
        string issuedBy,
        CancellationToken cancellationToken = default)
    {
        await lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            // Re-check inside the gate: a concurrent create could have replaced
            // the world between the endpoint's check and this point.
            if (engine.MapContext is not null && !request.Confirm)
                throw new WorldResetConfirmationRequiredException();

            var startTimestamp = Stopwatch.GetTimestamp();

            var previousTick = engine.CurrentTick;
            var previousMap = engine.BuildMapStatus();
            var oldMapId = string.IsNullOrWhiteSpace(previousMap.MapId) ? "world" : previousMap.MapId;
            var oldMapVersion = previousMap.Version;
            var oldPlacementIds = engine.MapContext?.Map.Placements.Keys
                .Select(networkId => networkId.Value)
                .ToList() ?? new List<ulong>();

            var playerIds = connectedPlayerIds
                .Concat(engine.Players.All().Select(profile => profile.PlayerId))
                .Where(playerId => !string.IsNullOrWhiteSpace(playerId))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            logger?.LogWarning(
                "Creating a new world (issued by {IssuedBy}): seed {Seed}, {Width}x{Height}, population {Population}, {PlayerCount} known player(s)",
                issuedBy,
                request.Seed,
                request.Width,
                request.Height,
                request.Population,
                playerIds.Count);

            StopPeriodicSave();
            if (engine.IsRunning)
                engine.Shutdown();
            networkIds.Clear();
            engine.ClearTransientState();

            engine.MapOptions = new MapOptions
            {
                MapId = "world",
                Version = Math.Max(1, previousMap.Version + 1),
                Seed = request.Seed,
                Width = request.Width,
                Height = request.Height,
                TileSize = 1f
            };
            engine.RandomSeed = request.PopulationSeed;
            engine.Initialize(startTick: previousTick + 1);
            StartPeriodicSave();

            PopulationGenerationResult? population = null;
            if (request.Population > 0)
            {
                try
                {
                    population = engine.GeneratePopulation(
                        request.Population,
                        request.PopulationSeed,
                        request.Adults,
                        request.Children);
                }
                catch (Exception ex)
                {
                    StopPeriodicSave();
                    engine.Shutdown();
                    networkIds.Clear();
                    logger?.LogError(
                        ex,
                        "World creation failed while generating a population for {Population} genoses (issued by {IssuedBy})",
                        request.Population,
                        issuedBy);
                    throw new InvalidOperationException(
                        $"The new map could not be populated: {ex.Message} Try a different seed or a larger map.",
                        ex);
                }
            }

            var playerGenosIds = new Dictionary<string, ulong>(StringComparer.Ordinal);
            foreach (var playerId in playerIds)
            {
                var genosNetworkId = engine.EnsurePlayerGenos(playerId);
                engine.EnsurePlayerSpawn(playerId);
                if (genosNetworkId != 0)
                    playerGenosIds[playerId] = genosNetworkId;
            }

            foreach (var networkId in oldPlacementIds)
                engine.Emit(new BuildingRemoved(oldMapId, oldMapVersion, networkId, engine.CurrentTick));

            SaveSnapshot();
            await SaveWorldDocumentsAsync();

            var mapStatus = engine.BuildMapStatus();
            var result = new CreateMapResult(
                mapStatus.MapId,
                mapStatus.Version,
                mapStatus.LayoutVersion,
                mapStatus.Seed,
                mapStatus.Width,
                mapStatus.Height,
                mapStatus.Buildings,
                population?.Total ?? 0,
                playerGenosIds.Count,
                (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);

            worldResetNotifier?.NotifyWorldReset(new WorldResetMessage
            {
                MapId = result.MapId,
                MapVersion = result.Version,
                LayoutVersion = result.LayoutVersion,
                Seed = result.Seed,
                Tick = engine.CurrentTick
            });

            logger?.LogWarning(
                "World reset complete (issued by {IssuedBy}): seed {Seed}, {Buildings} buildings, {Population} population, {Players} player(s) in {ElapsedMs} ms",
                issuedBy,
                result.Seed,
                result.Buildings,
                result.Population,
                result.Players,
                result.ElapsedMs);

            return result;
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    public ulong AddGenos(string name, int age)
    {
        var result = ExecuteAdminSync(
            new Genelife.Api.Commands.CreateGenosCommand(Guid.NewGuid(), "system", name, age));

        if (!result.Accepted || result.Payload is not ulong networkId)
            throw new InvalidOperationException(result.Error ?? "Failed to add genos.");

        return networkId;
    }

    public async Task<List<GenosStatus>> GetAllGenosesAsync()
    {
        if (readModelStore != null)
        {
            var genoses = await readModelStore.TryGetAllGenosesAsync();
            if (genoses != null)
                return genoses.ToList();
        }

        var snapshot = engine.GetLatestSnapshot();
        if (snapshot != null)
            return snapshot.Genoses;

        return engine.ExecuteWithWorld(world =>
        {
            var repository = new GenosRepository(world, networkIds, engine.Economy, engine.MapContext);
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

    public bool DeleteGenos(ulong networkId)
    {
        var result = ExecuteAdminSync(
            new Genelife.Api.Commands.DeleteGenosCommand(Guid.NewGuid(), "system", networkId));

        if (!result.Accepted)
            throw new InvalidOperationException(result.Error ?? "Failed to delete genos.");

        return result.Payload is bool deleted && deleted;
    }

    public void SetGenosAction(ulong networkId, string actionType)
    {
        var result = ExecuteAdminSync(
            new Genelife.Api.Commands.SetGenosActionCommand(Guid.NewGuid(), "system", networkId, actionType));

        if (!result.Accepted)
            throw new InvalidOperationException(result.Error ?? "Failed to set genos action.");
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
        lifecycleGate.Dispose();
    }
}