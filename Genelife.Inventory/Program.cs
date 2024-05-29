using Genelife.Inventory.Sagas;
using MassTransit;
using OpenTelemetry.Resources;
using System.Reflection;
using OpenTelemetry.Metrics;
using Microsoft.AspNetCore.Builder;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;
using Serilog.Sinks.Grafana.Loki;

static bool IsRunningInContainer() => bool.TryParse(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), out var inContainer) && inContainer;

static void ConfigureResource(ResourceBuilder r)
{
    r.AddService("Inventory",
        serviceVersion: "1",
        serviceInstanceId: Environment.MachineName);
}

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Debug)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx,cfg)=>
{
    //Override Few of the Configurations
    cfg.Enrich.WithProperty("Application", ctx.HostingEnvironment.ApplicationName)
        .Enrich.WithProperty("Environment", ctx.HostingEnvironment.EnvironmentName)
        .WriteTo.Console(new RenderedCompactJsonFormatter())
        .WriteTo.GrafanaLoki("http://lgtm:3100/");


});
builder.Services.AddOpenTelemetry()
.ConfigureResource(ConfigureResource)
.WithMetrics(b => b
    // MassTransit Meter
    .AddMeter("MassTransit")
    .AddOtlpExporter(o => {
        o.Endpoint = new Uri(IsRunningInContainer() ? "http://lgtm:4317" : "http://localhost:4317");
        o.Protocol = OtlpExportProtocol.Grpc;
    })
    .AddPrometheusExporter()
).WithTracing(b => b
    .AddSource("MassTransit")
    .SetResourceBuilder(ResourceBuilder.CreateDefault()
        .AddService("Inventory Service")
        .AddTelemetrySdk()
        .AddEnvironmentVariableDetector()
    )
    .AddOtlpExporter(o => {
        o.Endpoint = new Uri(IsRunningInContainer() ? "http://lgtm:4317" : "http://localhost:4317");
        o.Protocol = OtlpExportProtocol.Grpc;
    })
);
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMassTransit(x => {
    x.SetKebabCaseEndpointNameFormatter();
    var entryAssembly = Assembly.GetEntryAssembly();

    x.AddConsumers(entryAssembly);
    x.AddSaga<InventorySaga>(so => so.UseConcurrentMessageLimit(1)).MongoDbRepository(r =>
    {
        r.Connection = "mongodb://root:example@mongo:27017/";
        r.DatabaseName = "inventorydb";
    });
    x.AddSagaStateMachines(entryAssembly);
    x.AddSagas(entryAssembly);
    x.AddActivities(entryAssembly);

    x.UsingRabbitMq((context, cfg) =>
    {
        if (IsRunningInContainer())
            cfg.Host("rabbitmq");
        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();
app.UseOpenTelemetryPrometheusScrapingEndpoint();
app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseAuthorization();
app.Run();
