using FluentValidation;
using WE.IOTService.Domain.Store;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Models.Device;

public class UpdateDeviceModelValidator: AbstractValidator<UpdateDeviceModel>
{
    public UpdateDeviceModelValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Owner).NotEmpty();
    }
    public UpdateDeviceModelValidator(IDeviceRepository repository)
    {
        RuleFor(x => x.Name).MustAsync(async (name, cancellation) =>
        {
            var existed = await repository.GetPagedListAsync(x => x.Name == name);
            return existed != null;
        }).WithMessage("Device already existed.");
    }

}

public class UpdateDeviceModel: DeviceBaseModel
{
}

