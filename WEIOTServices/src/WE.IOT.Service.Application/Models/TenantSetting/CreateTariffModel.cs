using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Models.TenantSetting;

public class CreateTariffModelValidator : AbstractValidator<CreateTariffModel>
{
    public CreateTariffModelValidator()
    {
        RuleFor(x => x.TariffValue).NotEmpty();
        RuleFor(x => x.TariffUnit).NotEmpty();

    }
}

public class CreateTariffModel
{
    public string TariffValue { get; set; } = default!;
    public string StartDate { get; set;} = default!;
    public string TariffUnit { get; set; } = "kwH";
}

