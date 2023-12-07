using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Graph;
using WE.IOT.Service.Application.Dtos.Operation;

namespace WE.IOT.Service.Application.Dtos.TenantSetting;

public class PowersDto
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
    public List<string> All { get; set; }
    public List<PowerDto> Zones { get; set; }
}

public class PowerDto
{

    public string Name { get; set; }
    public List<string> DeviceIds { get; set; }
}

public class PowerDeviceDto
{
    public string DeviceName { get; set; }
    public string DeviceId { get; set;}
    public string Status { get; set; }
    public List<OperationDto> Commands {  get; set; }
}

public class ZoneDto
{
    public string ZoneName { get; set; }    
    public List<PowerDeviceDto> Devices { get; set; }
}

public class BatchDeviceDto
{
    public ZoneDto All { get; set; }
    public List<ZoneDto> Zones { get; set; }
}