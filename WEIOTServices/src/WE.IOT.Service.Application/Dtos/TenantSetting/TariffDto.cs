using Common.MongoDb.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Dtos.TenantSetting;

public class TariffDto: BaseDto
{
    public double TariffValue { get; set; }
    public string TariffUnit { get; set; }
}
