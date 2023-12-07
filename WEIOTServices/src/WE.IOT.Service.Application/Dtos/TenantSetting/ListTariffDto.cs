using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Dtos.TenantSetting;


[BsonIgnoreExtraElements]
public class TariffValue
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
    public string Year { get; set; } = default!;
    public string Month { get; set; } = default!;
    public double Value { get; set; } = default!;
    public string Unit { get; set; } = default!;    
}
