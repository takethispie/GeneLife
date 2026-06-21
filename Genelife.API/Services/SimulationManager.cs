using Genelife.Api.DTOs;
using Genelife.Api.Repositories;

namespace Genelife.Api.Services;

public class SimulationManager : IHostedService, IDisposable
{
    private readonly SimulationEngine engine;

    public SimulationManager()
    {
        engine = new SimulationEngine();
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
        engine.Start();
    }

    public void StopSimulation()
    {
        engine.Stop();
    }

    public void AddSim(string name, int age)
    {
        engine.ExecuteWithWorld(world =>
        {
            var repository = new SimRepository(world);
            repository.Add(name, age);
            return true;
        });
    }

    public List<SimStatus> GetAllSims()
    {
        return engine.ExecuteWithWorld(world =>
        {
            var repository = new SimRepository(world);
            return repository.GetAll();
        });
    }

    public int AddCompany(string name)
    {
        return engine.ExecuteWithWorld(world =>
        {
            var repository = new CompanyRepository(world);
            return repository.Add(name);
        });
    }

    public CompanyStatus? GetCompany(int entityId)
    {
        return engine.ExecuteWithWorld(world =>
        {
            var repository = new CompanyRepository(world);
            return repository.Get(entityId);
        });
    }

    public List<CompanyStatus> GetAllCompanies()
    {
        return engine.ExecuteWithWorld(world =>
        {
            var repository = new CompanyRepository(world);
            return repository.GetAll();
        });
    }

    public bool DeleteCompany(int entityId)
    {
        return engine.ExecuteWithWorld(world =>
        {
            var repository = new CompanyRepository(world);
            return repository.Delete(entityId);
        });
    }

    public void Dispose()
    {
        engine.Dispose();
    }
}
