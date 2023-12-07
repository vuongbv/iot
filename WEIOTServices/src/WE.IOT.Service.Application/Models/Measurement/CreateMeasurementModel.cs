using FluentValidation;

namespace WE.IOT.Service.Application.Models.Measurement;


public class CreateMeasurementModelValidator : AbstractValidator<CreateMeasurementModel>
{
    public CreateMeasurementModelValidator()
    {
        RuleFor(x => x.Payload).NotEmpty();
    }
}

public class CreateMeasurementModel: BaseMeasurementModel
{
    
}
