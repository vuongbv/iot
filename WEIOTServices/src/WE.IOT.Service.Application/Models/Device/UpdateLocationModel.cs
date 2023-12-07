using FluentValidation;
using WE.IOTService.Domain.Entities;
using WE.IOTService.Domain.Store;

namespace WE.IOT.Service.Application.Models.Device;

public class UpdateLocationModelValidator : AbstractValidator<UpdateLocationModel>
{
    public UpdateLocationModelValidator()
    {
        RuleFor(x => x.Source.Id).NotEmpty();
        RuleFor(x => x.GT_Position.Lat).NotEmpty();
        RuleFor(x => x.GT_Position.Lng).NotEmpty();
        RuleFor(x => x.GT_Position.Alt).NotEmpty();
    }

    public UpdateLocationModelValidator(IDeviceRepository repository)
    {
        RuleFor(x => x.Source.Id).MustAsync(async (id, cancellation) =>
        {
            var existed = repository.GetFirstOrDefault(x => x.Id == id);
            return existed == null;
        }).WithMessage("Device does not existed");
    }
}

public class UpdateLocationModel
{
    public Device Source { get; set; }
    public Position GT_Position { get; set; }
}

public class Device
{
    public string Id { get; set; }
}

