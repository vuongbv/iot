using FluentValidation;
using WE.IOTService.Domain.Store;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Models.AlarmSetting;

public class UpdateAlarmSettingModelValidator: AbstractValidator<UpdateAlarmSettingModel>
{
    public UpdateAlarmSettingModelValidator()
    {
        RuleFor(x => x.DeviceId).NotEmpty();
        RuleFor(x => x.Threshold).NotEmpty();
    }

}

public class UpdateAlarmSettingModel: AlarmSettingBaseModel
{
}

