using FluentValidation;
using WE.IOTService.Domain.Store;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Models.TenantSetting;

public class UpdateTenantSettingModelValidator: AbstractValidator<UpdateTenantSettingModel>
{
    public UpdateTenantSettingModelValidator()
    {
        RuleFor(x => x.TariffValue).NotEmpty();
        RuleFor(x => x.TariffUnit).NotEmpty();
    }

}

public class UpdateTenantSettingModel: TenantSettingBaseModel
{
}

