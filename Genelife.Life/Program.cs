using MassTransit;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using System.Reflection;
using Genelife.Life.Configuration;
using Genelife.Life.Sagas;
using Genelife.Life.Sagas.States;

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

CreateHostBuilder(args).Build().Run();

static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureServices((hostContext, services) => {
            MongoDbConfiguration.Configure();
            services.AddMassTransit(x =>
            {
                x.SetKebabCaseEndpointNameFormatter();
                var entryAssembly = Assembly.GetEntryAssembly();

                x.AddConsumers(entryAssembly);
                x.AddSagaStateMachine<HumanSaga, HumanSagaState>(so => so.UseConcurrentMessageLimit(1)).MongoDbRepository(r =>
                {
                    r.Connection = "mongodb://root:example@mongo:27017/";
                    r.DatabaseName = "maindb";
                });
                x.AddSagas(entryAssembly);
                x.AddActivities(entryAssembly);

                x.UsingRabbitMq((context, cfg) =>
                {
                    if (IsRunningInContainer())
                        cfg.Host("rabbitmq");
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
                        .AddService("Life")
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
