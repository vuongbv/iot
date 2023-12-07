using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Models.Operation;

public class CreateTurnOffModel
{
    public string Point { get; set; }
    public string PointStatus { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
}

