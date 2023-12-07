using FluentValidation;
using WE.IOTService.Domain.Store;

namespace WE.IOT.Service.Application.Models.Device;

public class CreateDeviceModelValidator : AbstractValidator<CreateDeviceModel>
{
    public CreateDeviceModelValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Owner).NotEmpty();
    }

    public CreateDeviceModelValidator(IDeviceRepository repository)
    {
        RuleFor(x => x.Name).MustAsync(async (name, cancellation) =>
        {
            var existed = repository.GetFirstOrDefault(x => x.Name == name);
            return existed == null;
        }).WithMessage("Device already existed");
    }
}

public class CreateDeviceModel : DeviceBaseModel
{
}

