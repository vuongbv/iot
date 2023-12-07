using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Dtos.Measurement
{
    public class TemperatureDto
    {
        public string Deviceid { get; set; }
        public DateTime Time { get; set; }
        public double? Enclosure { get; set; }
        public double? Solar { get; set; }
    }
    public class TemperatureRawDto
    {
        [BsonId]
        public ObjectId _id { get; set; }
        public string Deviceid { get; set; }
        public DateTime Time { get; set; }
        public double? Enclosure { get; set; }
        public double? Solar { get; set; }
    }
}
