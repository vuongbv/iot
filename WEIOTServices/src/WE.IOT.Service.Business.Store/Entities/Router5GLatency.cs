using Common.MongoDb.Entities;
using Common.MongoDb.Serializer;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using static WE.IOTService.Domain.Entities.RouterSignal;

namespace WE.IOTService.Domain.Entities;

public class Router5GLatency : BaseEntity
{
    public DateTime? time { get; set; }
    public DeviceSource source { get; set; }
    public string type { get; set; } 
    public string netdeviceid { get; set; }
    public DeviceRouterLatency Net_5GDeviceLatency { get; set; }
    public class DeviceRouterLatency
    {
        public Device5GData duration { get; set; }
        public Device5GData? jitter { get; set; }
        public Device5GData? latency { get; set; }
    }
}


