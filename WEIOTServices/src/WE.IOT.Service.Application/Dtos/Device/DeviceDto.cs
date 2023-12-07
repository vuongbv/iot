using Common.MongoDb.Entities;
using WE.IOTService.Domain.Entities;

namespace WE.IOTService.Application.Dto.Device
{

    public class DeviceDto: BaseDto
    {
        public string Name { get; set; } = default!;
        public string Owner { get; set; } = default!;
        public Position GT_Position { get; set; }
        public string? OperationStatus { get; set; }
        public string? SourceId { get; set; }
        public  string Status { get; set; }
        public string? Panel { get; set; } = String.Empty;
        public ExtInfo? Ext_Info { get; set; }
        public string? Uptime { get; set; }
    }
}
