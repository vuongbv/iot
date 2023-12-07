using WE.IOTService.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Models.AlarmSetting;

public class AlarmSettingBaseModel
{
    public string DeviceId { get; set; }
    public double Threshold { get; set; }
    public string? SourceName { get; set; }
    public string? SourceType { get; set; }
}
