using AutoMapper;
using Common.Mongodb.Collections;
using FluentValidation;
using WE.IOT.Service.Application.Constants;
using WE.IOT.Service.Application.Models.AlarmSetting;
using WE.IOT.Service.Application.Models.TenantSetting;
using WE.IOTService.Application.Dto.AlarmSetting;
using WE.IOTService.Application.Dto.TenantSetting;
using WE.IOTService.Domain.Entities;
using WE.IOTService.Domain.Store;

namespace WE.IOT.Service.Application.Services;

public interface IAlarmSettingService
{
    public Task<AlarmSettingDto> CreateAlarmSetting(CreateAlarmSettingModel request);
    public Task<bool> UpdateAlarmSetting(string id, UpdateAlarmSettingModel request);
    public Task<bool> DeleteAlarmSetting(string alarmSettingId);
    public Task<PagedList<AlarmSettingDto>> GetAllAlarmSetting(SearchAlarmSettingViewModel search);
    public AlarmSettingDto GetAlarmSetting(string id);
}

public class AlarmSettingService : IAlarmSettingService
{
    private readonly IAlarmSettingRepository _alarmSettingStore;
    private readonly ITenantSettingRepository _tenantSettingStore;
    private readonly  IDeviceRepository _deviceRepository;
        private readonly IMapper _mapper;

    public AlarmSettingService(IAlarmSettingRepository alarmSettingStore, ITenantSettingRepository tenantSettingStore, IMapper _mapper, IDeviceRepository deviceRepository)
    {
        this._alarmSettingStore = alarmSettingStore;
        this._tenantSettingStore = tenantSettingStore;
        this._mapper = _mapper;
        _deviceRepository = deviceRepository;
    }

    public async Task<AlarmSettingDto> CreateAlarmSetting(CreateAlarmSettingModel request)
    {
        var validator = await (new CreateAlarmSettingModelValidator()).ValidateAsync(request);

        if (!validator.IsValid)
        {
            throw new ValidationException(validator.Errors);
        }

        var AlarmSetting = _mapper.Map<WE.IOTService.Domain.Entities.AlarmSetting>(request);
        var result = _alarmSettingStore.Insert(AlarmSetting);
        var response = _mapper.Map<AlarmSettingDto>(result);
        return response;
    }

    public async Task<PagedList<AlarmSettingDto>> GetAllAlarmSetting (SearchAlarmSettingViewModel search)
    {
        var listResult = await _alarmSettingStore.GetAll().ToPagedList(search);
        var devices = _deviceRepository.GetAll().Where(x => (x.Status == null || x.Status == DeviceConstants.ActiveDevice));
        var result = _mapper.Map<PagedList<AlarmSettingDto>>(listResult);
        result.Items = result.Items.Select(x => new AlarmSettingDto()
        {
            DeviceId = x.DeviceId,
            DeviceName = devices.FirstOrDefault(i => i.Id == x.DeviceId).Name,
            SourceName = x.SourceName,
            SourceType = x.SourceType,
            Threshold = x.Threshold,
            Id = x.Id
        }).ToList();
        return result;
    }

    public async Task<bool> UpdateAlarmSetting(string id, UpdateAlarmSettingModel model)
    {
        var validator = await (new UpdateAlarmSettingModelValidator()).ValidateAsync(model);

        if (!validator.IsValid)
        {
            throw new ValidationException(validator.Errors);
        }

        var entity = _alarmSettingStore.GetFirstOrDefault(x => x.Id == id);

        if(entity != null)
        {
            var AlarmSetting = _mapper.Map(model, entity);
            AlarmSetting.UpdatedDate = DateTime.UtcNow;
            _alarmSettingStore.Update(AlarmSetting, x => x.Id == AlarmSetting.Id);
            return true;
        }
        
        return false;
    }

    public async Task<bool> DeleteAlarmSetting(string id)
    {
        await _alarmSettingStore.DeleteAsync(x => x.Id == id);
        return true;
    }
    public AlarmSettingDto GetAlarmSetting(string id)
    {
        var AlarmSetting = _alarmSettingStore.GetFirstOrDefault(x => x.Id == id);
        return _mapper.Map<AlarmSettingDto>(AlarmSetting);
    }
}
