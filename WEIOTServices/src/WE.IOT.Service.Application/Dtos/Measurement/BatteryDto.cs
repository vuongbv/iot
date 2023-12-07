using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Dtos.Measurement
{
    public class DeviceBatteryDto
    {
        public string? DeviceId { get; set; }
        public double? BatteryTotalConsumed { get; set; }
        public double? TotalPowerGenerated { get; set; }
        public double? BatteryCurrentVoltage { get; set; }
        public DateTime? DataTime { get; set; }
    }

    public class BatteryDto
    {
        public string? _id { get; set; }
        public double? TotalPower { get; set; }
    }

    public class VoltageDto
    {
        [BsonId]
        public ObjectId _id { get; set; }
        public DateTime Time { get; set; }
        public double? Voltage { get; set; }
    }
}
