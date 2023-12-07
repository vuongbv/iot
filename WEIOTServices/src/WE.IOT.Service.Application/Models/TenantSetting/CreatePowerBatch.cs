using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Models.TenantSetting;

public class CreatePowerBatchModel
{
    public List<PowerBatchModel> Zones { get; set; }
}
public class PowerBatchModel
{
    public string Name { get; set; }
    public List<string> DeviceIds { get; set; }
}
