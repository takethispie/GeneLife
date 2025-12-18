using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Genelife.Life.Interfaces;
using Genelife.Life.Domain.Activities;

namespace Genelife.Life.Configuration;

/// <summary>
/// Configures MongoDB BSON serialization for the Life domain
/// </summary>
public static class MongoDbConfiguration
{
    private static bool _isConfigured = false;
    private static readonly object _lock = new object();

    /// <summary>
    /// Configures MongoDB BSON serialization settings
    /// This method is idempotent and can be called multiple times safely
    /// </summary>
    public static void Configure()
    {
        if (_isConfigured)
            return;

        lock (_lock)
        {
            if (_isConfigured)
                return;

            ConfigureBasicSerializers();
            ConfigureLivingActivityPolymorphism();
            
            _isConfigured = true;
        }
    }

    private static void ConfigureBasicSerializers()
    {
        // Configure GUID serialization to use standard representation (only if not already registered)
        if (!BsonSerializer.SerializerRegistry.GetSerializer<Guid>().GetType().Name.Contains("GuidSerializer"))
        {
            try
            {
                BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
            }
            catch (BsonSerializationException)
            {
                // Serializer already registered, ignore
            }
        }
        
        // Allow object serialization for flexible document structure (only if not already registered)
        try
        {
            BsonSerializer.RegisterSerializer(new ObjectSerializer(ObjectSerializer.AllAllowedTypes));
        }
        catch (BsonSerializationException)
        {
            // Serializer already registered, ignore
        }
    }

    private static void ConfigureLivingActivityPolymorphism()
    {
        // Register all concrete implementations with their discriminators
        // MongoDB will automatically handle polymorphism when these are registered
        RegisterActivityType<Sleep>("Sleep");
        RegisterActivityType<Eat>("Eat");
        RegisterActivityType<Genelife.Life.Domain.Activities.Work>("Work");
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
                cm.SetIgnoreExtraElements(true); // Ignore unknown fields for forward compatibility
            });
        }
    }

    /// <summary>
    /// Gets all registered activity discriminators for debugging purposes
    /// </summary>
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