using Genelife.Physical.Repository;
using Genelife.Physical.Sagas;
using MassTransit;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using System.Reflection;

static bool IsRunningInContainer() => bool.TryParse(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), out var inContainer) && inContainer;

static void ConfigureResource(ResourceBuilder r)
{
    r.AddService("Physical",
        serviceVersion: "1",
        serviceInstanceId: Environment.MachineName);
}

const string facility = "physical-service";

var facilityLabel = new LokiLabel() { 
            Key = "genelife-server", 
            Value = facility };

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Debug)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.GrafanaLoki("http://localhost:3100", [facilityLabel])
    .CreateLogger();

await CreateHostBuilder(args).Build().RunAsync();

static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureServices((hostContext, services) =>
        {
            services.AddSingleton<GroceryShopCache>();            
            services.AddMassTransit(x =>
            {
                x.SetKebabCaseEndpointNameFormatter();
                var entryAssembly = Assembly.GetEntryAssembly();

                x.AddConsumers(entryAssembly);
                x.AddSaga<GroceryShopSaga>(so => {
                    so.UseConcurrentMessageLimit(1);
                }).MongoDbRepository(r =>
                {
                    r.Connection = "mongodb://root:example@mongo:27017/";
                    r.DatabaseName = "physicaldb";
                    r.CollectionName = "grocery-store";
                });
                x.AddSaga<HouseSaga>(so => {
                    so.UseConcurrentMessageLimit(1);
                }).MongoDbRepository(r =>
                {
                    r.Connection = "mongodb://root:example@mongo:27017/";
                    r.DatabaseName = "physicaldb";
                    r.CollectionName = "house-store";
                });
                x.AddActivities(entryAssembly);
                x.UsingRabbitMq((context, cfg) =>
                {
                    if (IsRunningInContainer())
                        cfg.Host("rabbitmq");
                    cfg.UseDelayedMessageScheduler();
                    cfg.ConfigureEndpoints(context);
                });
            });

            services.AddOpenTelemetry()
                .ConfigureResource(ConfigureResource)
                .WithMetrics(b => b
                    // MassTransit Meter
                    .AddMeter("MassTransit")
                    .AddOtlpExporter(o =>
                    {
                        o.Endpoint = new Uri(IsRunningInContainer() ? "http://lgtm:4317" : "http://localhost:4317");
                        o.Protocol = OtlpExportProtocol.Grpc;
                    })
                    .AddPrometheusExporter()
                ).WithTracing(b => b
                    .AddSource("MassTransit")
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService("Physical")
                        .AddTelemetrySdk()
                        .AddEnvironmentVariableDetector()
                    )
                    .AddOtlpExporter(o =>
                    {
                        o.Endpoint = new Uri(IsRunningInContainer() ? "http://lgtm:4317" : "http://localhost:4317");
                        o.Protocol = OtlpExportProtocol.Grpc;
                    })
                );
        });
