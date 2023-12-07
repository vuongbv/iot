using Common.MongoDb.Entities;
using Common.MongoDb.Serializer;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using static WE.IOTService.Domain.Entities.RouterSignal;

namespace WE.IOTService.Domain.Entities;

public class Router5GUpload : BaseEntity
{
    public DateTime? time { get; set; }
    public DeviceSource source { get; set; }
    public string type { get; set; }
    public string netdeviceid { get; set; }
    public DeviceRouterUpload Net_5GDeviceUpload { get; set; }
    public class DeviceRouterUpload
    {
        public Device5GData duration { get; set; }
        public Device5GData? throughput { get; set; }
        public Device5GData? server_received { get; set; }
    }
}


