
namespace Genelife.API;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddAuthorization();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapGet("/healthcheck", (HttpContext httpContext) =>
        {
            return Results.Ok();
        })
        .WithName("healthcheck")
        .WithOpenApi();

        app.Run();
    }
}
