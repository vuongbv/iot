using FluentValidation;
using WE.IOTService.Domain.Store;

namespace WE.IOT.Service.Application.Models.TenantSetting;

public class CreateTenantSettingModelValidator : AbstractValidator<CreateTenantSettingModel>
{
    public CreateTenantSettingModelValidator()
    {
        RuleFor(x => x.TariffValue).NotEmpty();
        RuleFor(x => x.TariffUnit).NotEmpty();
    }
}

public class CreateTenantSettingModel : TenantSettingBaseModel
{
}

