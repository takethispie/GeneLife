using Genelife.Physical.Repository;
using Genelife.Physical.Sagas;
using MassTransit;
using MassTransit.Monitoring;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using System.Reflection;

static bool IsRunningInContainer() => bool.TryParse(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), out var inContainer) && inContainer;

static void ConfigureResource(ResourceBuilder r)
{
    r.AddService("Physical",
        serviceVersion: "1",
        serviceInstanceId: Environment.MachineName);
}

await CreateHostBuilder(args).Build().RunAsync();

static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureServices((hostContext, services) =>
        {
            services.AddSingleton<GroceryShopRepository>();
            services.AddSingleton<HumanRepository>();
            services.AddMassTransit(x =>
            {
                x.SetKebabCaseEndpointNameFormatter();
                var entryAssembly = Assembly.GetEntryAssembly();

                x.AddConsumers(entryAssembly);
                x.AddSagaStateMachine<MoveSaga, MoveSagaState>().MongoDbRepository(r =>
                {
                    r.Connection = "mongodb://root:example@mongo:27017/";
                    r.DatabaseName = "physicaldb";
                    r.CollectionName = "move";
                });
                x.AddSaga<GroceryShopSaga>().MongoDbRepository(r =>
                {
                    r.Connection = "mongodb://root:example@mongo:27017/";
                    r.DatabaseName = "physicaldb";
                    r.CollectionName = "grocery-store";
                }); ;
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
                .AddMeter(InstrumentationOptions.MeterName)
                .AddPrometheusExporter()
            );
        });
