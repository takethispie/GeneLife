using Microsoft.AspNetCore.ResponseCompression;
using Genelife.Web.Server.Hubs;
using GeneLife;

namespace Genelife.Web;
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("origins",
                                  policy =>
                                  {
                                      policy.WithOrigins("http://localhost:7085")
                                                  .AllowAnyHeader()
                                                  .AllowAnyMethod();
                                  });
        });
        // Add services to the container.

        builder.Services.AddControllersWithViews();
        builder.Services.AddRazorPages();
        builder.Services.AddSignalR(o =>
        {
            o.EnableDetailedErrors = true;
        });
        builder.Services.AddResponseCompression(opts =>
        {
            opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
               new[] { "application/octet-stream" });
        });
        builder.Services.AddSingleton((services) => new GeneLifeSimulation());
        
        var app = builder.Build();
        app.UseCors("origins");
        app.UseResponseCompression();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles();

        app.UseRouting();


        app.MapRazorPages();
        app.MapControllers();
        app.MapHub<DataHub>("/datahub");
        app.MapFallbackToFile("index.html");
        app.Run();
    }
}
