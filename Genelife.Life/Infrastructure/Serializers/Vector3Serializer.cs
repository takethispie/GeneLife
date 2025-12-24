using System.Numerics;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Genelife.Life.Infrastructure.Serializers;

public class Vector3Serializer : SerializerBase<Vector3>
{
    public override Vector3 Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var document = BsonDocumentSerializer.Instance.Deserialize(context, args);
        
        var x = document.GetValue("X", 0.0).ToDouble();
        var y = document.GetValue("Y", 0.0).ToDouble();
        var z = document.GetValue("Z", 0.0).ToDouble();
        
        return new Vector3((float)x, (float)y, (float)z);
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Vector3 value)
    {
        var document = new BsonDocument
        {
            { "X", value.X },
            { "Y", value.Y },
            { "Z", value.Z }
        };
        
        BsonDocumentSerializer.Instance.Serialize(context, args, document);
    }
}