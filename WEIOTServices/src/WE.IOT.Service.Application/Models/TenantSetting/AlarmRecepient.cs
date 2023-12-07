using Common.Models;
using Microsoft.AspNetCore.Http;
using WE.IOT.Service.Application.Constants;

namespace WE.IOT.Service.Application.Models.TenantSetting;

public class AlarmRecepient
{
    public string? ListName { get; set; } = default!;
    public AlarmConstants.RecepientType? Type { get; set; }
    public List<string>? Recepient { get; set; }

}

public class AlarmRecepientSearch : SearchBaseViewModel
{

}