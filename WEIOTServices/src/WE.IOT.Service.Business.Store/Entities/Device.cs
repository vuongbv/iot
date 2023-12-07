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

    public class Device : BaseEntity
    {
        public string Name { get; set; } = default!;
        public string Owner { get; set;  } = default!;
        public string SourceId  { get; set; }
        public Position GT_Position { get; set; }
        public string? OperationStatus { get; set; } = "Off";
        public string Status { get; set; }
        public string? Panel { get; set; } = String.Empty;
        public ExtInfo? Ext_Info { get; set; }
    }

    public class Position{ 
        public decimal Lng { get; set; }
        public decimal Lat { get; set; }
        public int Alt { get; set; }
    }

    public class ExtInfo
    {
        public string? RouterId { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public List<string>? InterfaceId { get; set; }
    }
}
