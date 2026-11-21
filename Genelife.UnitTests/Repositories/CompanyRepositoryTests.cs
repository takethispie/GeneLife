using Arch.Core;
using Genelife.Api.Repositories;
using Genelife.Api.Services;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Enums;
using Genelife.Systems;

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

    [Fact]
    public void Delete_ClearsEmployeesAndIndexAfterHire()
    {
        var world = World.Create();
        var index = new EmploymentIndex();
        var registry = new NetworkIdRegistry();
        var repository = new CompanyRepository(world, index, registry);
        var networkId = repository.Add("Acme");
        Assert.True(registry.TryGetEntity(networkId, out var companyEntity));

        var office = world.Create(new Office("HQ", companyEntity));
        var posting = world.Create(new JobPosting(companyEntity, office, "Dev", 3000f));
        world.Create(new OpenPosition(posting));
        world.Create(
            new Alive(20),
            new JobSeeker(true, Genelife.Enums.Skill.None, 1000f)
        );

        var hiringSystem = new HiringSystem(world, index, 0);
        hiringSystem.Update();
        hiringSystem.Update();

        Assert.Equal(1, index.CountEmployees(companyEntity));

        var deleted = repository.Delete(networkId);
        Assert.True(deleted);
        Assert.Equal(0, index.TotalEmployees());
        Assert.True(repository.DebugInvariantPasses());
    }

    [Fact]
    public void DebugInvariant_PassesWithCleanWorld()
    {
        var world = World.Create();
        var repository = new CompanyRepository(world, new EmploymentIndex(), new NetworkIdRegistry());
        Assert.True(repository.DebugInvariantPasses());
    }
}
