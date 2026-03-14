using MassTransit;
using Serilog;
using System.Reflection;
using Genelife.Application.Sagas;
using Genelife.Application.Sagas.States;
using Genelife.Application.Services;
using Genelife.Domain.Activities;
using Genelife.Domain.Human.Activities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Debug)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Register Aspire-managed RabbitMQ connection (connection string injected by AppHost)
builder.AddRabbitMQClient("rabbitmq");

builder.Services.AddSingleton<ClockService>();

BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
BsonSerializer.RegisterSerializer(new ObjectSerializer(ObjectSerializer.AllAllowedTypes));
BsonClassMap.RegisterClassMap<Sleep>();
BsonClassMap.RegisterClassMap<Eat>();
BsonClassMap.RegisterClassMap<Work>();
BsonClassMap.RegisterClassMap<Shower>();
BsonClassMap.RegisterClassMap<Idle>();

var mongoConnectionString = builder.Configuration.GetConnectionString("maindb")
    ?? "mongodb://root:example@localhost:27017/";

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    var entryAssembly = Assembly.GetEntryAssembly();

    x.AddConsumers(entryAssembly);
    x.AddSagaStateMachine<HumanSaga, HumanSagaState>(so =>
    {
        so.UsePartitioner(20, ctx => ctx.CorrelationId ?? Guid.Empty);
        so.ConcurrentMessageLimit = 1;
    }).MongoDbRepository(r =>
    {
        r.Connection = mongoConnectionString;
        r.DatabaseName = "maindb";
    });

    x.AddSaga<HouseSaga>(so =>
    {
        so.UsePartitioner(20, ctx => ctx.CorrelationId ?? Guid.Empty);
        so.ConcurrentMessageLimit = 1;
    }).MongoDbRepository(r =>
    {
        r.Connection = mongoConnectionString;
        r.DatabaseName = "maindb";
    });

    x.AddSaga<GroceryStoreSaga>(so =>
    {
        so.UsePartitioner(20, ctx => ctx.CorrelationId ?? Guid.Empty);
        so.ConcurrentMessageLimit = 1;
    }).MongoDbRepository(r =>
    {
        r.Connection = mongoConnectionString;
        r.DatabaseName = "maindb";
    });

    x.AddSaga<CompanySaga>(so =>
    {
        so.UsePartitioner(20, ctx => ctx.CorrelationId ?? Guid.Empty);
        so.ConcurrentMessageLimit = 1;
    }).MongoDbRepository(r =>
    {
        r.Connection = mongoConnectionString;
        r.DatabaseName = "maindb";
    });

    x.AddSagaStateMachine<JobPostingSaga, JobPostingSagaState>(so =>
    {
        so.UsePartitioner(20, ctx => ctx.CorrelationId ?? Guid.Empty);
        so.ConcurrentMessageLimit = 1;
    }).MongoDbRepository(r =>
    {
        r.Connection = mongoConnectionString;
        r.DatabaseName = "maindb";
    });

    x.AddSagaStateMachine<WorkerSaga, WorkerSagaState>(so =>
    {
        so.UsePartitioner(20, ctx => ctx.CorrelationId ?? Guid.Empty);
        so.ConcurrentMessageLimit = 1;
    }).MongoDbRepository(r =>
    {
        r.Connection = mongoConnectionString;
        r.DatabaseName = "maindb";
    });

    x.AddSagas(entryAssembly);
    x.AddActivities(entryAssembly);

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.UseMessageRetry(r => r.Exponential(
            retryLimit: 5,
            minInterval: TimeSpan.FromMilliseconds(100),
            maxInterval: TimeSpan.FromSeconds(5),
            intervalDelta: TimeSpan.FromMilliseconds(200)
        ));
        cfg.UseInMemoryOutbox(context);
        cfg.UseDelayedMessageScheduler();
        cfg.ConfigureEndpoints(context);
    });
});

builder.Build().Run();
