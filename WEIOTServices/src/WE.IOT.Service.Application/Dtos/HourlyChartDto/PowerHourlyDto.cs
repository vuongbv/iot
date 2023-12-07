using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Dtos.Measurement
{
    public class PowerHourly
    {
        public int _id { get; set; }
        public double? TotalPower { get; set; }
    }

    public class PowerSunlightGeneratedHourlyDto
    {
        public List<HourlyDto> TotalPower { get; set; }
        public List<HourlyDto> SunLight { get; set; }
}
    public class HourlyDto
    {
        public DateTime _id { get; set; }
        public double? Value { get; set; }
    }

    public class LastHourlyDto
    {
        public DateTime _id { get; set; }
        public double? Value { get; set; }
    }
}
