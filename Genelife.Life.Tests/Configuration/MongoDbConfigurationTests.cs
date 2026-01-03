using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Genelife.Life.Configuration;
using Genelife.Life.Interfaces;
using Genelife.Life.Domain.Activities;
using Xunit;

namespace Genelife.Life.Tests.Configuration;

public class MongoDbConfigurationTests
{
    public MongoDbConfigurationTests()
    {
        // Ensure configuration is applied before each test
        MongoDbConfiguration.Configure();
    }

    [Fact]
    public void Configure_ShouldRegisterAllActivityTypes()
    {
        // Arrange & Act
        var registeredTypes = MongoDbConfiguration.GetRegisteredActivityTypes();

        // Assert
        Assert.Equal(5, registeredTypes.Count);
        Assert.Contains("Sleep", registeredTypes.Keys);
        Assert.Contains("Eat", registeredTypes.Keys);
        Assert.Contains("Work", registeredTypes.Keys);
        Assert.Contains("Shower", registeredTypes.Keys);
        Assert.Contains("Idle", registeredTypes.Keys);
    }

    [Theory]
    [InlineData(typeof(Sleep), "Sleep")]
    [InlineData(typeof(Eat), "Eat")]
    [InlineData(typeof(Shower), "Shower")]
    [InlineData(typeof(Idle), "Idle")]
    public void SerializeDeserialize_ShouldPreserveActivityType(Type activityType, string expectedDiscriminator)
    {
        // Arrange
        var activity = (ILivingActivity)Activator.CreateInstance(activityType)!;

        // Act - Serialize to BSON
        var bsonDocument = activity.ToBsonDocument();
        
        // Assert - Check discriminator is present
        Assert.True(bsonDocument.Contains("_t"));
        Assert.Equal(expectedDiscriminator, bsonDocument["_t"].AsString);

        // Act - Deserialize back to object
        var deserializedActivity = BsonSerializer.Deserialize<ILivingActivity>(bsonDocument);

        // Assert - Type should be preserved
        Assert.Equal(activityType, deserializedActivity.GetType());
        Assert.Equal(activity.TickDuration, deserializedActivity.TickDuration);
        Assert.Equal(activity.GoHomeWhenFinished, deserializedActivity.GoHomeWhenFinished);
    }

    [Fact]
    public void SerializeDeserialize_WorkActivity_ShouldHandleNamespaceConflict()
    {
        // Arrange
        var workActivity = new Genelife.Life.Domain.Activities.Work();

        // Act - Serialize to BSON
        var bsonDocument = workActivity.ToBsonDocument();
        
        // Assert - Check discriminator is correct
        Assert.True(bsonDocument.Contains("_t"));
        Assert.Equal("Work", bsonDocument["_t"].AsString);

        // Act - Deserialize back to object
        var deserializedActivity = BsonSerializer.Deserialize<ILivingActivity>(bsonDocument);

        // Assert - Should be the correct Work type from Activities namespace
        Assert.Equal(typeof(Genelife.Life.Domain.Activities.Work), deserializedActivity.GetType());
        Assert.True(deserializedActivity.GoHomeWhenFinished);
    }

    [Fact]
    public void Configure_CalledMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange & Act - Call configure multiple times
        MongoDbConfiguration.Configure();
        MongoDbConfiguration.Configure();
        MongoDbConfiguration.Configure();

        // Assert - Should not throw and should still work
        var sleepActivity = new Sleep();
        var bsonDocument = sleepActivity.ToBsonDocument();
        var deserializedActivity = BsonSerializer.Deserialize<ILivingActivity>(bsonDocument);
        
        Assert.Equal(typeof(Sleep), deserializedActivity.GetType());
    }

    [Fact]
    public void Deserialize_UnknownDiscriminator_ShouldThrowMeaningfulException()
    {
        // Arrange
        var invalidDocument = new BsonDocument
        {
            { "_t", "UnknownActivity" },
            { "TickDuration", 10 },
            { "GoHomeWhenFinished", false }
        };

        // Act & Assert
        var exception = Assert.Throws<BsonSerializationException>(() =>
            BsonSerializer.Deserialize<ILivingActivity>(invalidDocument));
        
        Assert.Contains("UnknownActivity", exception.Message);
    }
}