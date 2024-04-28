using Genelife.Survival.Sagas;
using MassTransit;
using System.Reflection;

static bool IsRunningInContainer() => bool.TryParse(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), out var inContainer) && inContainer;

await CreateHostBuilder(args).Build().RunAsync();

static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureServices((hostContext, services) =>
        {
            services.AddMassTransit(x =>
            {
                x.SetKebabCaseEndpointNameFormatter();
                var entryAssembly = Assembly.GetEntryAssembly();

                x.AddConsumers(entryAssembly);
                x.AddSagaStateMachines(entryAssembly);
                x.AddSaga<HungerSaga>().MongoDbRepository(r =>
                {
                    r.Connection = "mongodb://root:example@mongo:27017/";
                    r.DatabaseName = "survivaldb";
                    r.CollectionName = "hunger";
                });
                x.AddSaga<ThirstSaga>().MongoDbRepository(r =>
                {
                    r.Connection = "mongodb://root:example@mongo:27017/";
                    r.DatabaseName = "survivaldb";
                    r.CollectionName = "thirst";
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
        });
