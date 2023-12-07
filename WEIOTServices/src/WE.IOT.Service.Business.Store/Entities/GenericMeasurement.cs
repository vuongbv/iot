using Common.MongoDb.Entities;
using Common.MongoDb.Serializer;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WE.IOTService.Domain.Entities;

public class GenericMeasurement : BaseEntity
{
    [BsonElement("payload")]
    public BsonDocument Payload { get; set; } = default!;
}

