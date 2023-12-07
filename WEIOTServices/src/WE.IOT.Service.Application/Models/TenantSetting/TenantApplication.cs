using Common.Models;
using Microsoft.AspNetCore.Http;
using WE.IOT.Service.Application.Constants;

namespace WE.IOT.Service.Application.Models.TenantSetting;

public class TenantApplication
{
    public string DefaultApplication { get; set; }
    public List<string>? Application { get; set; }

}

public class TenantApplicationSearch : SearchBaseViewModel
{

}
public enum AppName
{
    SolarPowerManagement,
    SmartWaterMonitoring,
    AirQualityMonitoring,
    SurveillanceSecurity,
    SmartCCTVMonitoring,
    SmartRetailMonitoring,
    PowerMonitoringManagement
}