using System.Reflection;
using Genelife.Global.Services;
using MassTransit;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
        
static bool IsRunningInContainer() => bool.TryParse(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), out var inContainer) && inContainer;

static void ConfigureResource(ResourceBuilder r)
{
    r.AddService("Life",
        serviceVersion: "1",
        serviceInstanceId: Environment.MachineName);
}

const string facility = "life-service";

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
        .ConfigureServices((hostContext, services) => {
            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
            BsonSerializer.RegisterSerializer(new ObjectSerializer(ObjectSerializer.AllAllowedTypes));
            services.AddSingleton<ClockService>();
            services.AddMassTransit(x => {
                x.AddDelayedMessageScheduler();

                x.SetKebabCaseEndpointNameFormatter();

                // By default, sagas are in-memory, but should be changed to a durable
                // saga repository.
                x.SetInMemorySagaRepositoryProvider();

                var entryAssembly = Assembly.GetEntryAssembly();

                x.AddConsumers(entryAssembly);
                x.AddSagaStateMachines(entryAssembly);
                x.AddSagas(entryAssembly);
                x.AddActivities(entryAssembly);

                x.UsingRabbitMq((context, cfg) => {
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
                        o.Endpoint = new(IsRunningInContainer() ? "http://lgtm:4317" : "http://localhost:4317");
                        o.Protocol = OtlpExportProtocol.Grpc;
                    })
                    .AddPrometheusExporter()
                ).WithTracing(b => b
                    .AddSource("MassTransit")
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService("Main")
                        .AddTelemetrySdk()
                        .AddEnvironmentVariableDetector()
                    )
                    .AddOtlpExporter(o =>
                    {
                        o.Endpoint = new(IsRunningInContainer() ? "http://lgtm:4317" : "http://localhost:4317");
                        o.Protocol = OtlpExportProtocol.Grpc;
                    })
                );
        });
