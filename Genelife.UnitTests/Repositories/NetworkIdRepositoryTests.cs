using Arch.Core;
using Genelife.Api.Repositories;
using Genelife.Api.Services;
using Genelife.Components.Employment;
using Genelife.Components.Gen;

namespace Genelife.UnitTests.Repositories;

public class NetworkIdRepositoryTests
{
    [Fact]
    public void SimRepository_Add_AssignsNetworkIdToStatus()
    {
        var world = World.Create();
        var registry = new NetworkIdRegistry();
        var repository = new SimRepository(world, registry);

        repository.Add("Alice", 30);

        var sim = Assert.Single(repository.GetAll());
        Assert.NotEqual(0UL, sim.NetworkId);
        Assert.True(registry.TryGetEntity(new NetworkId(sim.NetworkId), out var entity));
        Assert.True(world.IsAlive(entity));
    }

    [Fact]
    public void CompanyRepository_AddGetAndGetAll_UseNetworkId()
    {
        var world = World.Create();
        var registry = new NetworkIdRegistry();
        var repository = new CompanyRepository(world, new EmploymentIndex(), registry);

        var networkId = repository.Add("Acme");

        var status = repository.Get(networkId);
        Assert.NotNull(status);
        Assert.Equal(networkId.Value, status!.NetworkId);
        Assert.Equal("Acme", status.Name);

        var all = Assert.Single(repository.GetAll());
        Assert.Equal(networkId.Value, all.NetworkId);
    }

    [Fact]
    public void CompanyRepository_Get_UnknownNetworkId_ReturnsNull()
    {
        var world = World.Create();
        var registry = new NetworkIdRegistry();
        var repository = new CompanyRepository(world, new EmploymentIndex(), registry);

        Assert.Null(repository.Get(new NetworkId(42)));
    }
}
