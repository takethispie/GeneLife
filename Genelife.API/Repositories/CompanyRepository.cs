using Arch.Core;
using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Gen;

namespace Genelife.Api.Repositories;

public class CompanyRepository
{
    private readonly World world;
    private readonly EmploymentIndex index;
    private readonly NetworkIdRegistry networkIds;

    public CompanyRepository(World world, EmploymentIndex index, NetworkIdRegistry networkIds)
    {
        this.world = world;
        this.index = index;
        this.networkIds = networkIds;
    }

    public NetworkId Add(string name)
    {
        var entity = world.Create(new CompanyName(name));
        var networkId = networkIds.Allocate(entity);
        world.Add(entity, networkId);
        return networkId;
    }

    public CompanyStatus? Get(NetworkId networkId)
    {
        if (!networkIds.TryGetEntity(networkId, out var entity))
            return null;

        if (!world.IsAlive(entity) || !world.Has<CompanyName>(entity))
            return null;

        ref var companyName = ref world.Get<CompanyName>(entity);
        return new CompanyStatus
        {
            NetworkId = networkId.Value,
            Name = companyName.Name,
            EmployeeCount = index.CountEmployees(entity)
        };
    }

    public List<CompanyStatus> GetAll()
    {
        var companies = new List<CompanyStatus>();
        var query = new QueryDescription().WithAll<CompanyName, NetworkId>();

        world.Query(in query, (Entity entity, ref CompanyName companyName, ref NetworkId networkId) =>
        {
            companies.Add(new CompanyStatus
            {
                NetworkId = networkId.Value,
                Name = companyName.Name,
                EmployeeCount = index.CountEmployees(entity)
            });
        });

        return companies;
    }

    public bool Delete(NetworkId networkId)
    {
        // Resolve via the registry rather than scanning with a raw Arch entity id.
        if (!networkIds.TryGetEntity(networkId, out var target))
            return false;

        if (!world.IsAlive(target) || !world.Has<CompanyName>(target))
            return false;

        world.Destroy(target);
        networkIds.Remove(networkId);
        return true;
    }
}
