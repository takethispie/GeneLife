using Genelife.Api.Services;
using Genelife.Api.DTOs;
using Genelife.Messages.Commands;
using Genelife.Messages.Queries;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;

namespace Genelife.Api.Endpoints.Wolverine;

public static class CompanyEndpoints
{
    [WolverinePost("/api/companies")]
    public static IResult CreateCompany(CreateCompany command, SimulationManager simulationManager)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Results.BadRequest(new { message = "Name is required" });

        try
        {
            var networkId = simulationManager.AddCompany(command.Name);
            return Results.Accepted("/api/companies/" + networkId, new { networkId, message = $"Company '{command.Name}' created successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    [WolverineGet("/api/companies")]
    public static List<CompanyStatus> GetAllCompanies(GetAllCompanies _, SimulationManager simulationManager)
    {
        return simulationManager.GetAllCompanies();
    }

    [WolverineGet("/api/companies/{networkId}")]
    public static IResult GetCompany(ulong networkId, SimulationManager simulationManager)
    {
        var company = simulationManager.GetCompany(networkId);
        if (company == null)
            return Results.NotFound(new { message = "Company not found" });

        return Results.Ok(company);
    }

    [WolverineDelete("/api/companies/{networkId}")]
    public static IResult DeleteCompany(ulong networkId, SimulationManager simulationManager)
    {
        try
        {
            var deleted = simulationManager.DeleteCompany(networkId);
            if (!deleted)
                return Results.NotFound(new { message = "Company not found" });

            return Results.Accepted();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }
}