using Common.Models;
using Microsoft.AspNetCore.Http;
using WE.IOT.Service.Application.Constants;

namespace WE.IOT.Service.Application.Models.TenantSetting;

public class LeftMenuSetting
{
    public List<LeftMenuSettingValue>? SettingValues { get; set; }

}

public class LeftMenuSettingSearch : SearchBaseViewModel
{

}

public class LeftMenuSettingValue
{
    public string? Menu { get; set; }
    public bool? Value { get; set; }
}