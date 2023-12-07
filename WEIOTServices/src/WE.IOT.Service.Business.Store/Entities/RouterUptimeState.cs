using Common.MongoDb.Entities;
using Common.MongoDb.Serializer;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WE.IOTService.Domain.Entities;

public class RouterUptimeState : BaseEntity
{
    public Net5GRouterUpTime Net_5GRouterUptime { get; set; }
    public DateTime? time { get; set; }
    public string created_at_timeuuid { get; set; }
    public SourceInfo source { get; set; }
    public string type { get; set; }

    public class Net5GRouterUpTime
    {
        public Period period { get; set; }
        public State state { get; set; }

    }

    public class Period
    {
        public double value { get; set; }
    }

    public class State
    {
        public string value { get; set; }
    }

    [BsonNoId]
    public class SourceInfo
    {
        public string id { get; set; }
    }
}


