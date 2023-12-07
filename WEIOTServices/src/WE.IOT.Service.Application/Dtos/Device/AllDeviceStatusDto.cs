using Common.MongoDb.Entities;
using WE.IOTService.Domain.Entities;

namespace WE.IOTService.Application.Dto.Device
{

    public class AllDeviceStatusDto
    {
        public int? Online { get; set; } =0;
        public int? Offline { get; set; } = 0;
    }
}
