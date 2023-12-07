using Common.MongoDb.Entities;
using Common.MongoDb.Serializer;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WE.IOTService.Domain.Entities;

public class RouterStatus
{
    public DateTime? time { get; set; }
    public Source source { get; set; }
    public string type { get; set; }
    public DeviceRouter Net_5GRouter { get; set; }
    [BsonNoId]
    public class Source
    {
        public string id { get; set; }
    }
    public class DeviceRouter
    {
        public DeviceRouterData name { get; set; }
        public DeviceRouterData? state { get; set; }
        public DateTime? updated_at { get; set; }
    }
    public class DeviceRouterData
    {
        public string value { get; set; }
    }
}


