using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Dtos.Measurement
{
    public class SingleChartQueryResult
    {
        public int? _id { get; set; }
        public DateTime? Point { get; set; }
        public double? Value { get; set; }
    }
    public class SingleChartQueryRawResult
    {
        public DateTime? _id { get; set; }
        public DateTime? Point { get; set; }
        public double? Value { get; set; }
    }
}
