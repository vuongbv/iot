using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Dtos.Measurement
{
    public class TotalPowerDto
    {
        public double? TotalPower { get; set; } = default!;
        public double? AvgCurrent { get; set; } = default!;
        public double? AvgVoltage { get; set; } = default!;
        public double? CurrentPotentialSaving { get; set; } = default!;
        public double? TotalPotentialSaving { get; set; } = default!;
        public DateTime? Time { get; set; }
    }
    public class PowerDto
    {
        public string _id { get; set; }
        public double TotalPowerChannel1 { get; set; } = default!;
        public double TotalPowerChannel2 { get; set; } = default!;
        public double TotalPowerChannel3 { get; set; } = default!;
        public double AvgVoltageChannel1 { get; set; } = default!;
        public double AvgVoltageChannel2 { get; set; } = default!;
        public double AvgVoltageChannel3 { get; set; } = default!;
        public double AvgCurrentChannel1 { get; set; } = default!;
        public double AvgCurrentChannel2 { get; set; } = default!;
        public double AvgCurrentChannel3 { get; set; } = default!;
    }

    public class GenericPowerDto
    {
        [BsonId]
        public ObjectId Id { get; set; }
        public double TotalPower { get; set; }
        public double MonthPotentialSaving { get; set; }
        public double? AvgCurrent { get; set; } = default!;
        public double? AvgVoltage { get; set; } = default!;
        public DateTime? Time;
    }

}
