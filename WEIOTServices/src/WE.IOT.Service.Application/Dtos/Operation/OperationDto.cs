using WE.IOT.Service.Application.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Dtos.Operation;

public class OperationDto
{
    public string Source { get; set; }
    public string? DeviceName { get; set; }
    public string CNO { get; set; }
    public string Point { get; set; }
    public string PointStatus { get; set; }
    public string Command { get; set; }
    public DateTimeOffset ExecutionTime { get; set; }
    public OperationStatus CommandStatus { get; set; }
}

