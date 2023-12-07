using AutoMapper;
using Common.Mongodb.Collections;
using Common.MongoDb.Audits;
using Common.MongoDb.Repository;
using Common.MongoDbMultitenant;
using FluentValidation;
using Microsoft.Graph;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using NetTopologySuite.Index.HPRtree;
using WE.IOT.Service.Application.Constants;
using WE.IOT.Service.Application.Dtos.Measurement;
using WE.IOT.Service.Application.Models.Device;
using WE.IOTService.Application.Dto.Device;
using WE.IOTService.Domain.Entities;
using WE.IOTService.Domain.Store;
using WE.IOT.Service.Domain.Repository;
using System.Text.Json;

namespace WE.IOT.Service.Application.Services;

public interface IDeviceService
{
    public Task<DeviceDto> CreateDevice(CreateDeviceModel request);
    public Task<bool> UpdateDevice(string id, UpdateDeviceModel request);
    public Task<bool> DeleteDevice(string deviceId);
    public Task<PagedList<DeviceDto>> GetAllDevice(SearchDeviceViewModel search);
    public DeviceDto GetDevice(string id);
    public bool UpdateLocation(UpdateLocationModel model);
    public bool UpdateExtinfo(UpdateExtinfoModel model);
    public Task<AllDeviceStatusDto> GetAllDeviceStatus();
    public Task<DeviceDto> GetDeviceSource(string sourceId);
    public Task<bool> CreateTrackingGps(CreateTrackingGpsModel request);
}

public class DeviceService : IDeviceService
{
    private readonly IDeviceRepository _deviceStore;
    private readonly IMapper _mapper;
    private readonly IMeasurementRepository _measurementRepository;
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IMultiTenantService<Common.Mongodb.MultiTenant.Tenant> _tenantService;

    public DeviceService(IDeviceRepository deviceStore, IMapper mapper, IMeasurementRepository measurementRepository, IRepositoryFactory repositoryFactory, IMultiTenantService<Common.Mongodb.MultiTenant.Tenant> tenantService)
    {
        this._deviceStore = deviceStore;
        this._mapper = mapper;
        this._measurementRepository = measurementRepository;
        this._repositoryFactory = repositoryFactory;
        this._tenantService = tenantService;
    }

    public async Task<AllDeviceStatusDto> GetAllDeviceStatus()
    {
        var tenant = await _tenantService.GetTenantAsync();
        var listResult = _deviceStore.GetAll().ToList();
        if (tenant.Name.Equals("nus"))
        {
            listResult = listResult.Where(x => x.Ext_Info != null && x.Ext_Info.Type != null && x.Ext_Info.Type.ToLower().Equals("router") && x.GT_Position != null && (x.GT_Position.Lat != 0 || x.GT_Position.Lng != 0)).ToList();
        }

        var result = new AllDeviceStatusDto()
        {
            Online = listResult.Count(x => x.OperationStatus.Equals(OperationConstants.StatusOn) && (x.Status == null || x.Status == DeviceConstants.ActiveDevice)),
            Offline = listResult.Count(x => x.OperationStatus.Equals(OperationConstants.StatusOff) && (x.Status == null || x.Status == DeviceConstants.ActiveDevice))
        };

        return result;
    }

    public async Task<DeviceDto> GetDeviceSource(string sourceId)
    {
        var measurementRespo = _repositoryFactory.GetRepository<BsonDocument>("Measurement");
        var device = _deviceStore.GetAll().FirstOrDefault(x => x.SourceId == sourceId);
        PipelineDefinition<BsonDocument, UptimeDto> pipeline5GUptime = new BsonDocument[]
        {
                        new BsonDocument("$match",
                        new BsonDocument
                            {
                                { "time",
                        new BsonDocument
                                {
                                    { "$gte",
                        DateTime.UtcNow.AddDays(-1) },
                                    { "$lte",
                        DateTime.UtcNow }
                                } },
                                { "source.id",
                        new BsonDocument("$eq", sourceId) },
                                { "type", "5G Device Signal" }
                            }),
                        new BsonDocument("$sort",
                        new BsonDocument("time", -1)),
                        new BsonDocument("$limit", 1),
                        new BsonDocument("$project",
                        new BsonDocument
                            {
                                { "Time", "$time" },
                                { "Uptime", "$Net_5GSignal.uptime.value" }
                            })
        };
        PipelineDefinition<BsonDocument, RouterStateDto> pipeline5GRouter = new BsonDocument[]
        {
                        new BsonDocument("$match",
                        new BsonDocument
                            {
                                { "time",
                        new BsonDocument
                                {
                                    { "$gte",
                        DateTime.UtcNow.AddDays(-1) },
                                    { "$lte",
                        DateTime.UtcNow }
                                } },
                                { "source.id",
                        new BsonDocument("$eq", sourceId) },
                                { "type", "5G Device Router" }
                            }),
                        new BsonDocument("$sort",
                        new BsonDocument("time", -1)),
                        new BsonDocument("$limit", 1),
                        new BsonDocument("$project",
                        new BsonDocument
                            {
                                { "Time", "$time" },
                                { "State", "Net_5GRouter.state.value" }
                            })
        };
        var data5gUptime = await measurementRespo.Collection.AggregateAsync<UptimeDto>(pipeline5GUptime);
        var data5gRouter = await measurementRespo.Collection.AggregateAsync<RouterStateDto>(pipeline5GRouter);
        var currentUpload = data5gUptime.ToList().OrderByDescending(x => x.Time);
        var currentState = data5gRouter.ToList().OrderByDescending(x => x.Time);
        var deviceDto =  _mapper.Map<DeviceDto>(device);
        if (currentUpload.Any())
        {
            deviceDto.Uptime = ConvertSecondsToDaysHoursMinutes(currentUpload.FirstOrDefault().Uptime);
        }
        if (currentState.Any())
        {
            // Fixed 16848
            deviceDto.UpdatedDate = currentState.FirstOrDefault().Time;
            //deviceDto.OperationStatus = currentState.FirstOrDefault().State.Equals("offline") ? "Off" : "On";
        }
            return deviceDto;
    }
    public static string ConvertSecondsToDaysHoursMinutes(int seconds)
    {
        TimeSpan ts = TimeSpan.FromSeconds(seconds);
        string result = string.Format("{0}{1}{2}",
        ts.Days > 0 ? ts.Days + " days " : "", // Show days only if greater than zero
        ts.Hours > 0 ? ts.Hours + " hours " : "", // Show hours only if greater than zero
        ts.Minutes > 0 ? ts.Minutes + " minutes " : "" // Show minutes only if greater than zero
        );

        // Return the formatted string
        return result;
    }

    public async Task<DeviceDto> CreateDevice(CreateDeviceModel request)
    {
        var validator = await (new CreateDeviceModelValidator(_deviceStore)).ValidateAsync(request);

        if (!validator.IsValid)
        {
            throw new ValidationException(validator.Errors);
        }

        var device = _mapper.Map<WE.IOTService.Domain.Entities.Device>(request);
        var result = _deviceStore.Insert(device);
        var response = _mapper.Map<DeviceDto>(result);
        return response;
    }

    private static bool WildcardMatch(string text, string pattern)
    {
        pattern = pattern.Replace("*", ".*");
        return System.Text.RegularExpressions.Regex.IsMatch(text, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    public async Task<PagedList<DeviceDto>> GetAllDevice(SearchDeviceViewModel search)
    {
        var lstInterface = new List<string>();

        //var listResult = await _deviceStore.GetAllWithDataAuthorize().Where(x => x.Status == null || x.Status == DeviceConstants.ActiveDevice).ToPagedList(search);
        var listResult = _deviceStore.GetAll()
            .Where(x => string.IsNullOrEmpty(search.RouterId) || (x.Ext_Info != null && x.Ext_Info.RouterId == search.RouterId))
            .ToList();

        //|| string.IsNullOrEmpty(search.InterfaceId) || (x.Ext_Info != null && x.Ext_Info.InterfaceId != null && x.Ext_Info.InterfaceId.Any(i => lstInterface.Any(item => item == i)))

        if (!string.IsNullOrEmpty(search.InterfaceId))
        {
            lstInterface = search.InterfaceId.Split(",").ToList();
            listResult = listResult.Where(x => x.Ext_Info != null && x.Ext_Info.InterfaceId != null && x.Ext_Info.InterfaceId.Any(i => lstInterface.Any(item => item == i))).ToList();
        }
        if (!string.IsNullOrEmpty(search.DeviceId))
        {
            var lstDevice = search.DeviceId.Split(",");
            listResult = listResult.Where(x => !lstDevice.Any() || lstDevice.Any(i => x.SourceId == i)).ToList();
        }
        if (!string.IsNullOrEmpty(search.DeviceName))
        {
            listResult = listResult.Where(item => (string.IsNullOrEmpty(search.DeviceName) || WildcardMatch(item.Name.ToLower(), search.DeviceName.ToLower()))).ToList();

        }

        var resultDto = _mapper.Map<List<DeviceDto>>(listResult);

        return new PagedList<DeviceDto>(resultDto.AsQueryable(), search);
    }


    public async Task<bool> UpdateDevice(string id, UpdateDeviceModel model)
    {
        var validator = await (new UpdateDeviceModelValidator(_deviceStore)).ValidateAsync(model);

        if (!validator.IsValid)
        {
            throw new ValidationException(validator.Errors);
        }

        var entity = _deviceStore.GetFirstOrDefault(x => x.Id == id);

        if(entity != null)
        {
            var device = _mapper.Map(model, entity);
            device.UpdatedDate = DateTime.UtcNow;
            _deviceStore.Update(device, x => x.Id == device.Id);
            return true;
        }
        
        return false;
    }

    public async Task<bool> DeleteDevice(string id)
    {
        await _deviceStore.DeleteAsync(x => x.Id == id);
        return true;
    }
    public DeviceDto GetDevice(string id)
    {
        //var device = _deviceStore.Find(x => x.SourceId == id && (x.Status == null || x.Status == DeviceConstants.ActiveDevice)).ToList().FirstOrDefault();
        //var device = _deviceStore.GetFirstOrDefault(x => x.SourceId == id && (x.Status == null || x.Status == DeviceConstants.ActiveDevice));
        var device = _deviceStore.GetFirstOrDefault(x => x.Id == id);
        return _mapper.Map<DeviceDto>(device);
    }
    public bool UpdateLocation(UpdateLocationModel model)
    {
        var device = _deviceStore.GetFirstOrDefault(x => x.Id == model.Source.Id);

        if (device != null)
        {
            device.UpdatedDate = DateTime.UtcNow;
            device.GT_Position = model.GT_Position;
            _deviceStore.Update(device, x => x.Id == device.Id);
            return true;
        }

        return false;
    }
    public bool UpdateExtinfo(UpdateExtinfoModel model)
    {
        var device = _deviceStore.GetFirstOrDefault(x => x.SourceId == model.Source.Id);

        if (device != null)
        {
            device.UpdatedDate = DateTime.UtcNow;
            device.Ext_Info = model.Ext_Info;
            _deviceStore.Update(device, x => x.Id == device.Id);
            return true;
        }

        return false;
    }

    public async Task<bool> CreateTrackingGps(CreateTrackingGpsModel request)
    {
        try
        {
            await _deviceStore.AuditCollection.InsertOneAsync(new Common.MongoDb.Audits.Audit()
            {
                Type = AuditType.TrackingGps,
                AfterValue = request != null ? BsonDocument.Parse(JsonSerializer.Serialize(request)) : BsonNull.Value,
            });
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }
}
