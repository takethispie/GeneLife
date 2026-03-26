using System.Numerics;
using Genelife.Application.Usecases;
using Genelife.Domain;
using Genelife.Domain.Work;
using Genelife.Domain.Work.Skills;
using Genelife.Messages.Commands;
using Genelife.Messages.Commands.Clock;
using Genelife.Messages.Commands.Company;
using Genelife.Messages.Commands.Human;
using Genelife.Messages.Commands.Worker;
using Genelife.Messages.Events.Buildings;
using MassTransit;
using Microsoft.AspNetCore.Mvc;

namespace Genelife.API.Endpoints;

public static class GenerationEndpointsExtension
{
    public static void UseGenerationEndpoints(this WebApplication app)
    {
        app.MapPost("/create/population/{humanCount}/{clockSpeedInMs}", async (int humanCount, int clockSpeedInMs, [FromServices] IPublishEndpoint endpoint) =>
        {
            var results = new
            {
                Humans = new List<object>(),
                Companies = new List<object>(),
                Houses = new List<object>(),
                Offices = new List<object>(),
                GroceryStore = new List<object>(),
            };
            
            var groceryId = Guid.NewGuid();
            await endpoint.Publish(new GroceryStoreBuilt(groceryId, 300, 300, 300, "intermarche"));
            results.GroceryStore.Add(new { Id = groceryId, Name = "intermache" });

            // Create humans with random sex distribution and houses for them
            for (int i = 0; i < humanCount; i++)
            {
                var houseLocation = new Vector3(
                    Random.Shared.NextSingle() * 1000 - 500,
                    Random.Shared.NextSingle() * 1000 - 500,
                    0
                );
                var sex = Random.Shared.Next(2) == 0 ? Sex.Male : Sex.Female;
                var humanId = Guid.NewGuid();
                var human = HumanGenerator.Build(humanId, sex);
                human.SetPosition(new Position(houseLocation.X, houseLocation.Y, houseLocation.Z));
                //TODO add randomization of stats
                await endpoint.Publish(new CreateHuman(humanId, human));
                results.Humans.Add(new { HumanId = humanId, Human = human });
                await endpoint.Publish(new CreateWorker(
                    Guid.NewGuid(),
                    humanId,
                    human.FirstName,
                    human.LastName,
                    new SkillSet())
                );
                
                var houseId  = Guid.NewGuid();
                await endpoint.Publish(new HouseBuilt(
                    houseId,
                    houseLocation.X,
                    houseLocation.Y,
                    houseLocation.Z,
                    [humanId]
                ));

                var coords = new Position(
                    houseLocation.X,
                    houseLocation.Y,
                    houseLocation.Z
                );
                
                results.Houses.Add(new {
                    HouseId = houseId,
                    Location = coords,
                    Owners = new List<Guid> { humanId }
                });
            }

            var companyTypes = Enum.GetValues<CompanyType>().Shuffle().Take(2).ToArray();
            foreach (var companyType in companyTypes)
            {
                var company = CompanyGenerator.Generate(companyType);
                var companyId = Guid.NewGuid();

                var officeLocation = new Vector3(
                    Random.Shared.NextSingle() * 800 - 400, 
                    Random.Shared.NextSingle() * 800 - 400,
                    0
                );
                
                var officeId = Guid.NewGuid();
                await endpoint.Publish(new CreateCompany(companyId, company, officeLocation.X, officeLocation.Y, officeLocation.Z));
                results.Companies.Add(new { CompanyId = companyId, Company = company });
                
                results.Offices.Add(new {
                    OfficeId = officeId,
                    CompanyId = companyId,
                    Name = $"{company.Name} Headquarters",
                    Location = officeLocation
                });
            }
            
            await endpoint.Publish(new SetClockSpeed(clockSpeedInMs));
            await endpoint.Publish(new StartClock());
            
            groceryId = Guid.NewGuid();
            await endpoint.Publish(new GroceryStoreBuilt(groceryId, 300, 300, 300, "carrefour"));
            results.GroceryStore.Add(new { Id = groceryId, Name = "carrefour" });

            return Results.Ok(new
            {
                Message = $"Created {humanCount} humans with houses, {companyTypes.Length} companies with offices, set clock speed to 100ms, started simulation",
                CreatedHumans = humanCount,
                CreatedCompanies = companyTypes.Length,
                CreatedHouses = humanCount,
                CreatedOffices = companyTypes.Length,
                Details = results
            });
        })
        .WithName("create Population");
    }
}