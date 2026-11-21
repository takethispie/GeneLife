using Genelife.Api.DTOs;
using Genelife.Api.Repositories;
using Genelife.Components.Gen;

namespace Genelife.Api.Services;

public class SimulationManager : IHostedService, IDisposable
{
    private readonly SimulationEngine engine;
    private readonly NetworkIdRegistry networkIds;

    public SimulationManager()
    {
        engine = new SimulationEngine();
        networkIds = new NetworkIdRegistry();
    }

    public bool IsRunning => engine.IsRunning;

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
        if (engine.IsRunning)
            return;

        networkIds.Clear();
        engine.Start();
    }

    public void StopSimulation()
    {
        if (!engine.IsRunning)
            return;

        engine.Stop();
        networkIds.Clear();
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
        return engine.ExecuteWithWorld(world =>
        {
            var repository = new CompanyRepository(world, GetIndex(), networkIds);
            return repository.Get(new NetworkId(networkId));
        });
    }

    public List<CompanyStatus> GetAllCompanies()
    {
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

    public void Dispose()
    {
        engine.Dispose();
    }
}
