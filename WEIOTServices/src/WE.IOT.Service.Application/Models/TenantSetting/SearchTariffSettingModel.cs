using Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Models.TenantSetting;

public class SearchTariffSettingModel: SearchBaseViewModel
{
    public DateTime? TariffStartDate { get; set; }
}
