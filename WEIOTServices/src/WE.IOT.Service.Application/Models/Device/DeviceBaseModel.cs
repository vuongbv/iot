using WE.IOTService.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Models.Device;

public class DeviceBaseModel
{
    public string Name { get; set; } = default!;
    public string? SourceId { get; set; }
    public string Owner { get; set; } = default!;
    public Position GT_Position { get; set; }
    public string? OperationStatus { get; set; } = "On";
    public string? Status { get; set; } = "active";
    public string? Panel { get; set; }

}
