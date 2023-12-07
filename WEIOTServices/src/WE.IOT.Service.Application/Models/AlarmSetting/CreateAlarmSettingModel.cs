using FluentValidation;
using WE.IOTService.Domain.Store;

namespace WE.IOT.Service.Application.Models.AlarmSetting;

public class CreateAlarmSettingModelValidator : AbstractValidator<CreateAlarmSettingModel>
{
    public CreateAlarmSettingModelValidator()
    {
        RuleFor(x => x.DeviceId).NotEmpty();
        RuleFor(x => x.Threshold).NotEmpty();
    }
}

public class CreateAlarmSettingModel : AlarmSettingBaseModel
{
}

