using FluentValidation;
using WE.IOTService.Domain.Entities;
using WE.IOTService.Domain.Store;

namespace WE.IOT.Service.Application.Models.Device;

public class UpdateExtinfoModelValidator : AbstractValidator<UpdateExtinfoModel>
{
    public UpdateExtinfoModelValidator()
    {
        RuleFor(x => x.Source.Id).NotEmpty();
    }

    public UpdateExtinfoModelValidator(IDeviceRepository repository)
    {
        RuleFor(x => x.Source.Id).MustAsync(async (id, cancellation) =>
        {
            var existed = repository.GetFirstOrDefault(x => x.Id == id);
            return existed == null;
        }).WithMessage("Device does not existed");
    }
}

public class UpdateExtinfoModel
{
    public Device Source { get; set; }
    public ExtInfo Ext_Info { get; set; }
}


