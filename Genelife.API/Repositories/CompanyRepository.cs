using Arch.Core;
using Genelife.Api.DTOs;
using Genelife.Components.Company;
using Genelife.Components.Employment;

namespace Genelife.Api.Repositories;

public class CompanyRepository
{
    private readonly World world;

    public CompanyRepository(World world)
    {
        this.world = world;
    }

    public int Add(string name)
    {
        var entity = world.Create(new CompanyName(name));
        return entity.Id;
    }

    public CompanyStatus? Get(int entityId)
    {
        CompanyStatus? result = null;
        var query = new QueryDescription().WithAll<CompanyName>();

        world.Query(in query, (Entity entity, ref CompanyName companyName) =>
        {
            if (entity.Id == entityId)
            {
                result = new CompanyStatus
                {
                    EntityId = entityId,
                    Name = companyName.Name,
                    EmployeeCount = CountEmployees(entity)
                };
            }
        });

        return result;
    }

    public List<CompanyStatus> GetAll()
    {
        var companies = new List<CompanyStatus>();
        var query = new QueryDescription().WithAll<CompanyName>();

        world.Query(in query, (Entity entity, ref CompanyName companyName) =>
        {
            companies.Add(new CompanyStatus
            {
                EntityId = entity.Id,
                Name = companyName.Name,
                EmployeeCount = CountEmployees(entity)
            });
        });

        return companies;
    }

    public bool Delete(int entityId)
    {
        bool deleted = false;
        var query = new QueryDescription().WithAll<CompanyName>();

        world.Query(in query, (Entity entity, ref CompanyName _) =>
        {
            if (entity.Id == entityId)
            {
                world.Destroy(entity);
                deleted = true;
            }
        });

        return deleted;
    }

    private int CountEmployees(Entity company)
    {
        int count = 0;
        var indexQuery = new QueryDescription().WithAll<EmploymentIndex>();
        world.Query(in indexQuery, (ref EmploymentIndex index) =>
        {
            count = index.CountEmployees(company);
        });
        return count;
    }
}
