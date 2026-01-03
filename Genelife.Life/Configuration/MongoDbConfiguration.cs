using System.Numerics;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Genelife.Life.Interfaces;
using Genelife.Life.Domain.Activities;

namespace Genelife.Life.Configuration;

public static class MongoDbConfiguration
{
    private static bool isConfigured;
    private static readonly Lock @lock = new();
    
    public static void Configure()
    {
        lock (@lock)
        {
            if (isConfigured)
                return;

            ConfigureBasicSerializers();
            ConfigureLivingActivityPolymorphism();
            
            isConfigured = true;
        }
    }

    private static void ConfigureBasicSerializers()
    {
        BsonSerializer.RegisterSerializer(new ObjectSerializer(ObjectSerializer.AllAllowedTypes));
        if (BsonSerializer.SerializerRegistry.GetSerializer<Guid>().GetType().Name.Contains("GuidSerializer")) return;
        try
        {
            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        }
        catch (BsonSerializationException)
        {
            // Serializer already registered, ignore
        }
    }

    private static void ConfigureLivingActivityPolymorphism()
    {
        RegisterActivityType<Sleep>("Sleep");
        RegisterActivityType<Eat>("Eat");
        RegisterActivityType<Domain.Activities.Work>("Work");
        RegisterActivityType<Shower>("Shower");
        RegisterActivityType<Idle>("Idle");
    }

    private static void RegisterActivityType<T>(string discriminator) where T : class, ILivingActivity
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(T)))
        {
            BsonClassMap.RegisterClassMap<T>(cm =>
            {
                cm.SetDiscriminator(discriminator);
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
            });
        }
    }
    
    public static IReadOnlyDictionary<string, Type> GetRegisteredActivityTypes()
    {
        return new Dictionary<string, Type>
        {
            { "Sleep", typeof(Sleep) },
            { "Eat", typeof(Eat) },
            { "Work", typeof(Genelife.Life.Domain.Activities.Work) },
            { "Shower", typeof(Shower) },
            { "Idle", typeof(Idle) }
        };
    }
}