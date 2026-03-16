var builder = DistributedApplication.CreateBuilder(args);

var mongo = builder.AddMongoDB("mongo")
    .WithMongoExpress();

var mongoDb = mongo.AddDatabase("maindb");

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin();

builder.AddProject<Projects.Genelife_API>("genelife-api")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq);

builder.AddProject<Projects.Genelife_Application>("genelife-application")
    .WithReference(rabbitmq)
    .WithReference(mongoDb)
    .WaitFor(rabbitmq)
    .WaitFor(mongo);

builder.Build().Run();
