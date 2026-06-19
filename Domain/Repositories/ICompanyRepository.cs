using System.Numerics;
using Domain.Entities;

namespace Domain.Repositories;

public interface ICompanyRepository {
    IEnumerable<Company> GetCompanies();
    IEnumerable<Company> GetCompaniesNear(Vector3 position, float radius);
    Company GetCompany(Guid companyId);
    Guid CreateCompany(Company company);
    bool DeleteCompany(Guid companyId);
    bool UpdateCompany(Company company);
}