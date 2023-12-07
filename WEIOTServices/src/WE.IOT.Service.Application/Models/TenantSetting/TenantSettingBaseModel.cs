using WE.IOTService.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.MongoDb.Entities;

namespace WE.IOT.Service.Application.Models.TenantSetting;

public class TenantSettingBaseModel
{
    public double TariffValue { get; set; }
    public string? TariffUnit { get; set; }

    public DateTime? DataLastSync { get; set; }
}
