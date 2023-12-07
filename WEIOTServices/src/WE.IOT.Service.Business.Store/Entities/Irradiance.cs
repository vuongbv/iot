using Common.MongoDb.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOTService.Domain.Entities
{
    public class Irradiance
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public int ghi { get; set; }
        public int ebh { get; set; }
        public int dni { get; set; }
        public int dhi { get; set; }
        public int cloud_opacity { get; set; }
        public decimal? lat { get; set; }
        public decimal? lng { get; set; }
        public string? sources { get; set; }
        public string? period_end { get; set; }
        public string? period { get; set; }
    }
}
