using Genelife.Api.DTOs;
using Genelife.Api.Repositories;

namespace Genelife.Api.Services;

public class SimulationManager : IHostedService, IDisposable
{
    private readonly SimulationEngine _engine;

    public SimulationManager()
    {
        _engine = new SimulationEngine();
    }

    public bool IsRunning => _engine.IsRunning;

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
        _engine.Start();
    }

    public void StopSimulation()
    {
        _engine.Stop();
    }

    public void AddSim(string name, int age)
    {
        _engine.ExecuteWithWorld(world =>
        {
            var repository = new SimRepository(world);
            repository.Add(name, age);
            return true;
        });
    }

    public List<SimStatus> GetAllSims()
    {
        return _engine.ExecuteWithWorld(world =>
        {
            var repository = new SimRepository(world);
            return repository.GetAll();
        });
    }

    public int AddCompany(string name)
    {
        return _engine.ExecuteWithWorld(world =>
        {
            var repository = new CompanyRepository(world);
            return repository.Add(name);
        });
    }

    public CompanyStatus? GetCompany(int entityId)
    {
        return _engine.ExecuteWithWorld(world =>
        {
            var repository = new CompanyRepository(world);
            return repository.Get(entityId);
        });
    }

    public List<CompanyStatus> GetAllCompanies()
    {
        return _engine.ExecuteWithWorld(world =>
        {
            var repository = new CompanyRepository(world);
            return repository.GetAll();
        });
    }

    public bool DeleteCompany(int entityId)
    {
        return _engine.ExecuteWithWorld(world =>
        {
            var repository = new CompanyRepository(world);
            return repository.Delete(entityId);
        });
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}
