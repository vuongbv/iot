using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Dtos.TenantSetting;

[BsonIgnoreExtraElements]
public class LogoDto
{
    public string Base64String { get; set; } = default!;
}

