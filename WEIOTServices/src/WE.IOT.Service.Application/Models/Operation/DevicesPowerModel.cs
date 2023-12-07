using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Models.Operation;

public class DevicesPowerModel
{
    public string DeviceId { get; set; }
    public string PinNumber { get; set; }
    public DevicePowerStatus PinState { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
}

