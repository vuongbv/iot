using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IotService.Application.Models;

public class MeasurementSignalrModel
{
    public string PanelId { get; set; } = default!;
    public string DeviceId { get; set; } = default!;
    public string ChartId { get; set; } = default!;
    public string WidgetId { get; set; } = default!;
}

