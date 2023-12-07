using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Models.Measurement;


public class UpdateMeasurementModelValidator : AbstractValidator<UpdateMeasurementModel>
{
    public UpdateMeasurementModelValidator()
    {
        RuleFor(x => x.Payload).NotEmpty();
    }
}

public class UpdateMeasurementModel : BaseMeasurementModel
{

}
