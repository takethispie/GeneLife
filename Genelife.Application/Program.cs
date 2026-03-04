using MassTransit;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using System.Reflection;
using Genelife.Application.Sagas;
using Genelife.Application.Sagas.States;
using Genelife.Application.Services;
using Genelife.Domain.Activities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

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
            services.AddSingleton<ClockService>();
            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
            BsonSerializer.RegisterSerializer(new ObjectSerializer(ObjectSerializer.AllAllowedTypes));
            BsonClassMap.RegisterClassMap<Sleep>();
            BsonClassMap.RegisterClassMap<Eat>();
            BsonClassMap.RegisterClassMap<Work>();
            BsonClassMap.RegisterClassMap<Shower>();
            BsonClassMap.RegisterClassMap<Idle>();
            
            services.AddMassTransit(x =>
            {
                x.SetKebabCaseEndpointNameFormatter();
                var entryAssembly = Assembly.GetEntryAssembly();

                x.AddConsumers(entryAssembly);
                x.AddSagaStateMachine<HumanSaga, HumanSagaState>(so => 
                {
                    so.UsePartitioner(8, ctx => ctx.CorrelationId ?? Guid.Empty);
                }).InMemoryRepository();
                x.AddSaga<HouseSaga>(so =>
                {
                    so.UsePartitioner(8, ctx => ctx.CorrelationId ?? Guid.Empty);
                }).InMemoryRepository();
                x.AddSaga<GroceryStoreSaga>(so => 
                {
                    so.UsePartitioner(8, ctx => ctx.CorrelationId ?? Guid.Empty);
                }).InMemoryRepository();
                x.AddSaga<CompanySaga>(so =>
                {
                    so.UsePartitioner(8, ctx => ctx.CorrelationId ?? Guid.Empty);
                }).InMemoryRepository();
                x.AddSagaStateMachine<JobPostingSaga, JobPostingSagaState>(so =>
                {
                    so.UsePartitioner(8, ctx => ctx.CorrelationId ?? Guid.Empty);
                }).InMemoryRepository();
                x.AddSagaStateMachine<WorkerSaga, WorkerSagaState>(so =>
                {
                    so.UsePartitioner(8, ctx => ctx.CorrelationId ?? Guid.Empty);
                }).InMemoryRepository();
                x.AddSagas(entryAssembly);
                x.AddActivities(entryAssembly);

                x.UsingRabbitMq((context, cfg) =>
                {
                    if (IsRunningInContainer())
                        cfg.Host("rabbitmq");
                    cfg.UseMessageRetry(r => r.Exponential(
                        retryLimit: 5,
                        minInterval: TimeSpan.FromMilliseconds(100),
                        maxInterval: TimeSpan.FromSeconds(5),
                        intervalDelta: TimeSpan.FromMilliseconds(200)
                    ));
                    cfg.UseInMemoryOutbox(context);
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
