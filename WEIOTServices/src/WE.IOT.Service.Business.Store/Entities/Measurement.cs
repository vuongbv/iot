using Common.MongoDb.Entities;
using Common.MongoDb.Serializer;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WE.IOTService.Domain.Entities;

public class Measurement : BaseEntity
{
    //[BsonElement("Payload")]
    //[BsonJson]
    public WatsonMeasurementPayload Payload { get; set; } = default!;

    public class WatsonMeasurementPayload
    {
        public DateTime Time { get; set; }
        public Device Source { get; set; }
        public string Type { get; set; }
        public Mcx_pv Channel1 { get; set; }
        public Mcx_pv Channel2 { get; set; }
        public Mcx_pv Channel3 { get; set; }
    }

    public class Device
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }
    public class Mcx_pv
    {
        public Data Current { get; set; }
        public Data Voltage { get; set; }
        public Data Power { get; set; }
    }
    public class Data
    {
        public string Unit { get; set; }
        public decimal? Value { get; set; }
    }
}

