using Microsoft.AspNetCore.Http;

namespace WE.IOT.Service.Application.Models.TenantSetting;

public class CreateOrUpdateSettingModel
{
    // for tariff
    public string? unit { get; set; } = default!;
    public double? value { get; set; } = default!;

    public IFormFile? FormFile { get; set; }

}