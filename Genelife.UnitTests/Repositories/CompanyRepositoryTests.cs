using Arch.Core;
using Genelife.Api.Repositories;
using Genelife.Api.Services;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Gen;

namespace Genelife.UnitTests.Repositories;

public class CompanyRepositoryTests
{
    [Fact]
    public void Delete_RemovesTargetCompanyAfterQuery()
    {
        var world = World.Create();
        var registry = new NetworkIdRegistry();
        var repository = new CompanyRepository(world, new EmploymentIndex(), registry);
        var networkId = repository.Add("Acme");
        Assert.True(registry.TryGetEntity(networkId, out var entity));

        Assert.True(world.IsAlive(entity));

        var deleted = repository.Delete(networkId);

        Assert.True(deleted);
        Assert.False(world.IsAlive(entity));
        Assert.Null(repository.Get(networkId));
        Assert.DoesNotContain(repository.GetAll(), company => company.NetworkId == networkId.Value);
        Assert.False(registry.TryGetEntity(networkId, out _));
    }

    [Fact]
    public void Delete_LeavesOtherCompaniesAndArchetypesIntact()
    {
        var world = World.Create();
        var registry = new NetworkIdRegistry();
        var repository = new CompanyRepository(world, new EmploymentIndex(), registry);
        var acmeId = repository.Add("Acme");
        var globexId = repository.Add("Globex");
        Assert.True(registry.TryGetEntity(globexId, out var globexEntity));

        Assert.True(repository.Delete(acmeId));

        Assert.True(world.IsAlive(globexEntity));
        Assert.True(world.Has<CompanyName>(globexEntity));

        var remaining = Assert.Single(repository.GetAll());
        Assert.Equal(globexId.Value, remaining.NetworkId);
        Assert.Equal("Globex", remaining.Name);

        var globex = repository.Get(globexId);
        Assert.NotNull(globex);
        Assert.Equal("Globex", globex.Name);
    }

    [Fact]
    public void Delete_ReturnsFalseForUnknownCompany()
    {
        var world = World.Create();
        var registry = new NetworkIdRegistry();
        var repository = new CompanyRepository(world, new EmploymentIndex(), registry);
        var networkId = repository.Add("Acme");
        Assert.True(registry.TryGetEntity(networkId, out var entity));

        var deleted = repository.Delete(new NetworkId(networkId.Value + 1000));

        Assert.False(deleted);
        Assert.True(world.IsAlive(entity));
        Assert.Single(repository.GetAll());
    }
}
