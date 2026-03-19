using Genelife.API.DTOs;
using Genelife.Messages.Commands.Grocery;
using Genelife.Messages.Events.Buildings;
using MassTransit;
using Microsoft.AspNetCore.Mvc;

namespace Genelife.API.Endpoints;

public static class BuildingEndpointsExtension
{
    public static void UseBuildingEndpoints(this WebApplication app)
    {
        app.MapPost("/create/house", async ([FromBody] CreateHouseRequest request, [FromServices] IPublishEndpoint endpoint) =>
            {
                var owners = new List<Guid> { request.HumanId };
                if (request.AdditionalOwners != null)
                {
                    owners.AddRange(request.AdditionalOwners);
                }

                await endpoint.Publish(new HouseBuilt(
                    request.HumanId,
                    request.Location.X,
                    request.Location.Y,
                    request.Location.Z,
                    owners
                ));
    
                return Results.Ok(new {
                    Message = "House created successfully",
                    request.HumanId,
                    request.Location,
                    Owners = owners
                });
            })
            .WithName("create House");

        app.MapPost("/create/grocery-store", async ([FromBody] CreateGroceryStoreRequest request, [FromServices] IPublishEndpoint endpoint) =>
            {
                var groceryStoreId = Guid.NewGuid();
                await endpoint.Publish(new GroceryStoreBuilt(
                    groceryStoreId,
                    request.X,
                    request.Y,
                    request.Z,
                    request.Name,
                    request.FoodPrice,
                    request.DrinkPrice
                ));
    
                return Results.Ok(new {
                    Message = "Grocery store created successfully",
                    GroceryStoreId = groceryStoreId,
                    Name = request.Name,
                    Location = new { request.X, request.Y, request.Z }
                });
            })
            .WithName("create GroceryStore");

        app.MapPatch("/grocery-store/{id:guid}/price", async (
                Guid id,
                [FromBody] SetGroceryPriceRequest request,
                [FromServices] IPublishEndpoint endpoint) =>
            {
                await endpoint.Publish(new SetGroceryPrice(id, request.FoodPrice, request.DrinkPrice));
                return Results.Accepted();
            })
            .WithName("set GroceryStore price");
    }
}

public record SetGroceryPriceRequest(int? FoodPrice, int? DrinkPrice);