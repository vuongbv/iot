using Common.Models;

namespace WE.IOT.Service.Application.Models.TenantSetting;

public class GetTenantSettingModel
{
    public string Key { get; set; } = default!;

    // for tariff setting
    public DateTime? TariffStartDate { get; set; } = default!;
}