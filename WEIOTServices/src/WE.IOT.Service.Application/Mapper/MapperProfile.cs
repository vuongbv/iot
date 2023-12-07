using AutoMapper;
using MongoDB.Bson;
using WE.IOT.Service.Application.Dto.Measurement;
using WE.IOT.Service.Application.Models.Device;
using WE.IOT.Service.Application.Models.Measurement;
using WE.IOTService.Application.Dto.Device;
using Common.MongoDb;
using Common.Mongodb.Collections;
using MongoDB.Bson.Serialization;
using Newtonsoft.Json;
using NLog.Layouts;
using WE.IOTService.Application.Dto.AlarmSetting;
using WE.IOTService.Application.Dto.TenantSetting;
using WE.IOT.Service.Application.Models.AlarmSetting;
using WE.IOT.Service.Application.Models.TenantSetting;
using WE.IOT.Service.Domain.Entities;
using WE.IOT.Service.Application.Dtos.Operation;
using WE.IOT.Service.Application.Dtos.TenantSetting;
using WE.IOTService.Domain.Entities;
using Common.Identity.Users;
using System.Text.Json.Nodes;

namespace WE.IOTService.Application.Common;

public class MapperProfile : Profile
{
    public MapperProfile()
    {
        CreateMap<WE.IOTService.Domain.Entities.Device, DeviceDto>();
        CreateMap<PagedList<WE.IOTService.Domain.Entities.Device>,PagedList<DeviceDto>>();
        CreateMap<CreateDeviceModel, WE.IOTService.Domain.Entities.Device>();
        CreateMap<UpdateDeviceModel, WE.IOTService.Domain.Entities.Device>();
        CreateMap<WE.IOTService.Domain.Entities.AlarmSetting, AlarmSettingDto>();
        CreateMap<PagedList<WE.IOTService.Domain.Entities.AlarmSetting>,PagedList< AlarmSettingDto>>();
        CreateMap<CreateAlarmSettingModel, WE.IOTService.Domain.Entities.AlarmSetting>();
        CreateMap<UpdateAlarmSettingModel, WE.IOTService.Domain.Entities.AlarmSetting>();

        CreateMap<WE.IOTService.Domain.Entities.TenantSetting, TenantSettingDto>();
        CreateMap<CreateTenantSettingModel, WE.IOTService.Domain.Entities.TenantSetting>();
        CreateMap<UpdateTenantSettingModel, WE.IOTService.Domain.Entities.TenantSetting>();


        CreateMap<CreateMeasurementModel, WE.IOTService.Domain.Entities.GenericMeasurement>()
            .ForMember(x => x.Payload, src => src.MapFrom(x => BsonDocument.Parse(x.Payload)));

        CreateMap<UpdateMeasurementModel, WE.IOTService.Domain.Entities.GenericMeasurement>()
            .ForMember(x => x.Payload, src => src.MapFrom(x => BsonDocument.Parse(x.Payload)));

        CreateMap<WE.IOTService.Domain.Entities.Measurement, MeasurementDto>();
        CreateMap<WE.IOTService.Domain.Entities.Measurement, WatsonMeasurementModel>();

        CreateMap<WatsonMeasurementModel, dynamic>();

        CreateMap<WE.IOTService.Domain.Entities.GenericMeasurement, MeasurementDto>()
            .ForMember(x => x.Measurement, payload => payload.MapFrom(x => x.Payload.ToDynamic()));

   
        CreateMap<Operation, OperationDto>();
        CreateMap<BsonValue, AlarmRecepientDto>();
        CreateMap<TenantSetting, AlarmRecepientDto>();
        
        CreateMap<IPagedList<TenantSetting>, IPagedList<AlarmRecepientDto>>().ForMember(i=>i.Items,d=>d.MapFrom(n=>n.Items));
        CreateMap<BsonValue, PagedList<CreateOrUpdateThresholdSettingDTO>>();
  
       
    }
}