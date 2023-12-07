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

    public class AlarmSetting : BaseEntity
    {
        public string DeviceId { get; set; }
        public double Threshold { get; set; }
        public string SourceName { get; set; }
        public string SourceType { get; set; }
    }
}
