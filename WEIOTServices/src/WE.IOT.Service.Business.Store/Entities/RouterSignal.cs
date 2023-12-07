using Common.MongoDb.Entities;
using Common.MongoDb.Serializer;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WE.IOTService.Domain.Entities;

public class RouterSignal : BaseEntity
{
    public DateTime? time { get; set; }
    public DeviceSource source { get; set; }
    public string type { get; set; }
    public string created_at_timeuuid { get; set; }
    public DeviceSignal Net_5GSignal { get; set; }
    [BsonNoId]
    public class DeviceSource
    {
        public string id { get; set; }
    }
    public class DeviceSignal
    {
        public Device5GData rsrq5g { get; set; }
        public Device5GData? rsrp5g { get; set; }
        public Device5GData? sinr5g { get; set; }
        public Device5GData? signal_percent { get; set; }
        public Device5GData? uptime { get; set; }
    }
    public class Device5GData
    {
        public double? value { get; set; }
    }
}


