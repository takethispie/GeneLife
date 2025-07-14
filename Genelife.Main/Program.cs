using Genelife.Main.Consumers;
using Genelife.Main.Sagas;
using Genelife.Main.Services;
using MassTransit;
using MassTransit.Monitoring;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using System.Reflection;
using Genelife.Main.Usecases;
using Genelife.Domain.Generators;

static bool IsRunningInContainer() => bool.TryParse(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), out var inContainer) && inContainer;

static void ConfigureResource(ResourceBuilder r)
{
    r.AddService("Main",
        serviceVersion: "1",
        serviceInstanceId: Environment.MachineName);
}

const string facility = "main-service";

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
            services.AddSingleton<ClockService>();
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
                x.AddSagaStateMachine<CompanySaga, CompanySagaState>(so => so.UseConcurrentMessageLimit(1)).MongoDbRepository(r =>
                {
                    r.Connection = "mongodb://root:example@mongo:27017/";
                    r.DatabaseName = "maindb";
                });
                x.AddSagaStateMachine<JobPostingSaga, JobPostingSagaState>(so => so.UseConcurrentMessageLimit(1)).MongoDbRepository(r =>
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
