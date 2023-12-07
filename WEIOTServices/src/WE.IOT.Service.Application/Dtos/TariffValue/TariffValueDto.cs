using Common.MongoDb.Entities;
using WE.IOTService.Domain.Entities;

namespace WE.IOTService.Application.Dto.TenantSetting
{

    public class TenantSettingDto: BaseDto
    {
        public double TariffValue { get; set; }
        public string TariffUnit { get; set; }
    }
}
