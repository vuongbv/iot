using Common.MongoDb.Entities;
using WE.IOTService.Domain.Entities;

namespace WE.IOTService.Application.Dto.AlarmSetting
{

    public class AlarmSettingDto: BaseDto
    {
        public string DeviceName { get; set; }
        public string DeviceId { get; set; }
        public double Threshold { get; set; }
        public string SourceName { get; set; }
        public string SourceType { get; set; }
    }
}
