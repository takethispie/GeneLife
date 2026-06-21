using Arch.Core;
using Genelife.Api.DTOs;
using Genelife.Components;

namespace Genelife.Api.Repositories;

public class CompanyRepository
{
    private readonly World _world;

    public CompanyRepository(World world)
    {
        _world = world;
    }

    public int Add(string name)
    {
        var entity = _world.Create(new CompanyName(name));
        return entity.Id;
    }

    public CompanyStatus? Get(int entityId)
    {
        CompanyStatus? result = null;
        var query = new QueryDescription().WithAll<CompanyName>();
        
        _world.Query(in query, (Entity entity, ref CompanyName companyName) =>
        {
            if (entity.Id == entityId)
            {
                int employeeCount = CountEmployees(entityId);
                result = new CompanyStatus
                {
                    EntityId = entityId,
                    Name = companyName.Name,
                    EmployeeCount = employeeCount
                };
            }
        });

        return result;
    }

    public List<CompanyStatus> GetAll()
    {
        var companies = new List<CompanyStatus>();
        var query = new QueryDescription().WithAll<CompanyName>();

        _world.Query(in query, (Entity entity, ref CompanyName companyName) =>
        {
            int employeeCount = CountEmployees(entity.Id);
            companies.Add(new CompanyStatus
            {
                EntityId = entity.Id,
                Name = companyName.Name,
                EmployeeCount = employeeCount
            });
        });

        return companies;
    }

    public bool Delete(int entityId)
    {
        bool deleted = false;
        var query = new QueryDescription().WithAll<CompanyName>();
        
        _world.Query(in query, (Entity entity, ref CompanyName _) =>
        {
            if (entity.Id == entityId)
            {
                _world.Destroy(entity);
                deleted = true;
            }
        });

        return deleted;
    }

    private int CountEmployees(int companyEntityId)
    {
        int count = 0;
        var employeeQuery = new QueryDescription().WithAll<Employee>();
        _world.Query(in employeeQuery, (ref Employee employee) =>
        {
            if (employee.CompanyEntityId == companyEntityId)
                count++;
        });
        return count;
    }
}
