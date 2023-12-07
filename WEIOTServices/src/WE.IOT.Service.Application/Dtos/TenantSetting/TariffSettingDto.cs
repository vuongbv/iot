using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Dtos.TenantSetting;

public class TariffSettingDto
{
    public string Id { get; set; }
    public string DateTime { get; set; }
    public double Value { get; set; }
    public string Unit { get; set; }
}

