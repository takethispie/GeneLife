using Genelife.Api.Services;
using Genelife.Api.DTOs;

namespace Genelife.Api.Endpoints;

public static class CompanyEndpoints
{
    public static WebApplication MapCompanyEndpoints(this WebApplication app)
    {
        app.MapPost("/api/companies", (AddCompanyRequest request, SimulationManager simulationManager) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { message = "Name is required" });

            try
            {
                var entityId = simulationManager.AddCompany(request.Name);
                return Results.Ok(new { message = $"Company '{request.Name}' created successfully", entityId });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .WithName("CreateCompany");

        app.MapGet("/api/companies/{entityId:int}", (int entityId, SimulationManager simulationManager) =>
        {
            var company = simulationManager.GetCompany(entityId);
            if (company == null)
                return Results.NotFound(new { message = "Company not found" });

            return Results.Ok(company);
        })
        .WithName("GetCompany");

        app.MapGet("/api/companies", (SimulationManager simulationManager) =>
        {
            var companies = simulationManager.GetAllCompanies();
            return Results.Ok(companies);
        })
        .WithName("GetAllCompanies");

        app.MapDelete("/api/companies/{entityId:int}", (int entityId, SimulationManager simulationManager) =>
        {
            var deleted = simulationManager.DeleteCompany(entityId);
            if (!deleted)
                return Results.NotFound(new { message = "Company not found" });

            return Results.Ok(new { message = "Company deleted successfully" });
        })
        .WithName("DeleteCompany");

        return app;
    }
}
