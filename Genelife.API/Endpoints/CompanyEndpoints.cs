using Genelife.Api.Auth;
using Genelife.Api.Services;
using Genelife.Api.DTOs;

namespace Genelife.Api.Endpoints;

[Obsolete("Migrated to Wolverine HTTP endpoints. See Endpoints/Wolverine/CompanyEndpoints.cs")]
public static class CompanyEndpoints
{
    public static void MapCompanyEndpoints(this WebApplication app)
    {
        app.MapPost("/api/companies", (AddCompanyRequest request, SimulationManager simulationManager) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { message = "Name is required" });

            try
            {
                var networkId = simulationManager.AddCompany(request.Name);
                return Results.Ok(new { message = $"Company '{request.Name}' created successfully", networkId });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .WithName("CreateCompany")
        .RequireAuthorization(AuthPolicies.Admin);

        app.MapGet("/api/companies/{networkId}", async (ulong networkId, SimulationManager simulationManager) =>
        {
            var company = await simulationManager.GetCompanyAsync(networkId);
            if (company == null)
                return Results.NotFound(new { message = "Company not found" });

            return Results.Ok(company);
        })
        .WithName("GetCompany")
        .RequireAuthorization(AuthPolicies.Player);

        app.MapGet("/api/companies", async (SimulationManager simulationManager) =>
        {
            var companies = await simulationManager.GetAllCompaniesAsync();
            return Results.Ok(companies);
        })
        .WithName("GetAllCompanies")
        .RequireAuthorization(AuthPolicies.Player);

        app.MapDelete("/api/companies/{networkId}", (ulong networkId, SimulationManager simulationManager) =>
        {
            var deleted = simulationManager.DeleteCompany(networkId);
            if (!deleted)
                return Results.NotFound(new { message = "Company not found" });

            return Results.Ok(new { message = "Company deleted successfully" });
        })
        .WithName("DeleteCompany")
        .RequireAuthorization(AuthPolicies.Admin);
    }
}
