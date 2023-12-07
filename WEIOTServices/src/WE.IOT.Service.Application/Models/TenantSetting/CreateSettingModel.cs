using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Models.TenantSetting;

public class CreateTariffSettingModel
{
    public string yrmonth { get; set; } = default!;
    public double value { get; set; } = default!;
    public string unit { get; set; } = default!;

}
