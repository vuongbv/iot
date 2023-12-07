using Common.Models;
using Microsoft.AspNetCore.Http;
using WE.IOT.Service.Application.Constants;

namespace WE.IOT.Service.Application.Models.TenantSetting;

public class ChartStyleSetting
{
    public string? ChartTypeName { get; set; }
    public List<SettingValue>? SettingValues { get; set; }

}

public class ChartStyleSettingSearch : SearchBaseViewModel
{

}

public class SettingValue
{
    public double? ValueFrom { get; set; }
    public double? ValueTo { get; set; }
    public string? Description { get; set; }
    public string Color { get; set; }
}
public class ChartStyleSettingDto
{
    public string Id { get; set; }
    public ChartStyleSetting ChartStyleSetting { get; set; }
}