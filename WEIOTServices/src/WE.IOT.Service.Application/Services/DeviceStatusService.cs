using WE.IOT.Service.Application.Constants;
using WE.IOTService.Application.Dto.Device;
using WE.IOTService.Domain.Store;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Services;

public interface IDeviceStatusService
{
    public Task<List<string>> GetActiveDevice(List<string>? deviceIds);
}

public class DeviceStatusService: IDeviceStatusService
{
    private readonly IDeviceRepository _deviceRepository;
    public DeviceStatusService(IDeviceRepository _deviceRepository)
    {
        this._deviceRepository = _deviceRepository;
    }
    public async Task<List<string>> GetActiveDevice(List<string>? deviceIds)
    {
        var listDevice = (await _deviceRepository.FindAsyncWithoutDataAuthorize(x =>
                ((deviceIds == null || deviceIds.Count < 1) && (string.IsNullOrEmpty(x.Status) ||
                         x.Status == DeviceConstants.ActiveDevice)) || (
                deviceIds.Contains(
                        x.SourceId) &&
                        (string.IsNullOrEmpty(x.Status) ||
                         x.Status == DeviceConstants.ActiveDevice)))).ToList();

        return listDevice.Select(x => x.SourceId).ToList();
    }

}
