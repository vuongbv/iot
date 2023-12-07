using AutoMapper;
using Common.Mongodb.Collections;
using FluentValidation;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using MimeKit.Encodings;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Queries;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Utilities;
using WE.IOT.Service.Application.Constants;
using WE.IOT.Service.Application.Dtos.Operation;
using WE.IOT.Service.Application.Dtos.TenantSetting;
using WE.IOT.Service.Application.Models.TenantSetting;
using WE.IOTService.Application.Dto.TenantSetting;
using WE.IOTService.Domain.Entities;
using WE.IOTService.Domain.Store;
using WE.IOT.Service.Domain.Entities;
using WE.IOT.Service.Domain.Repository;
using System;
using System.Collections;
using System.Text.Json;
using static MongoDB.Driver.WriteConcern;
using static System.Net.Mime.MediaTypeNames;

namespace WE.IOT.Service.Application.Services;

public interface ITenantSettingService
{
    public Task<TenantSettingDto> CreateTenantSetting(CreateTenantSettingModel request);
    public Task<bool> UpdateTenantSetting(UpdateTenantSettingModel request);
    public Task<bool> DeleteTenantSetting(string? tenantSettingId, string? key);
    public Task<PagedList<TenantSettingDto>> GetAllTenantSetting(SearchTenantSettingViewModel search);
    public TenantSettingDto GetTenantSetting();
    public Task<string> GetTenantLogo();
    public Task CreateLogoSetting(string base64String);
    public Task<TariffValue> GetTarifSetting(DateTime? dateTime);
    public Task CreateSetting(CreateTariffSettingModel model, string? tariffType);
    public Task<List<TariffValue>> GetListTariffSetting(SearchTariffSettingModel model, string? tariffType);
    public Task<TenantSetting> CreateThresholdSetting(CreateOrUpdateThresholdSettingModel model);
    public Task UpdateThresholdSetting(string? id, CreateOrUpdateThresholdSettingModel model);
    public Task<TenantSetting> CreateAlarmRecepient(AlarmRecepient model);
    public Task UpdateAlarmRecepient(string id, AlarmRecepient model);
    public Task<AlarmRecepient> GetAlarmRecepient(string id);
    Task<PagedList<AlarmRecepientDto>> SearchAlarmRecepient(AlarmRecepientSearch search);
    public Task<TenantSetting> CreateTenantApplication(TenantApplication model);
    public Task UpdateTenantApplication(TenantApplication model);
    public Task<TenantApplication> GetTenantApplication();
    public Task<PagedList<CreateOrUpdateThresholdSettingDTO>> GetThreshold(TenantApplicationSearch search);
    public Task<CreateOrUpdateThresholdSettingDTO> GetThresholdByKey(string id);
    public Task<BatchDeviceDto> GetPowersSetting();
    public Task CreatePowersSetting(CreatePowerBatchModel model);
    public Task<TenantSetting> CreateChartStyleSetting(ChartStyleSetting model);
    public Task UpdateChartStyleSetting(ChartStyleSetting model, string id);
    public Task<List<ChartStyleSettingDto>> GetChartStyleSetting(string? chartTypeName);
    public Task<ChartStyleSettingDto> GetChartStyleSettingById(string? id);
    public Task<TenantSetting> CreateLeftMenuSetting(LeftMenuSetting model);
    public Task UpdateLeftMenuSetting(LeftMenuSetting model);
    public Task<LeftMenuSetting> GetLeftMenuSetting();
}

public class TenantSettingService : ITenantSettingService
{
    private readonly ITenantSettingRepository _tenantSettingStore;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IMapper _mapper;
    private readonly IOperationRepository _operationRespository;
    public TenantSettingService(
        ITenantSettingRepository tenantSettingStore, 
        IDeviceRepository deviceRepository, 
        IMapper mapper,
        IOperationRepository operationRespository)
    {
        this._tenantSettingStore = tenantSettingStore;
        _deviceRepository = deviceRepository;
        this._mapper = mapper;
        this._operationRespository = operationRespository;
    }

    public async Task<TenantSettingDto> CreateTenantSetting(CreateTenantSettingModel request)
    {
        var validator = await (new CreateTenantSettingModelValidator()).ValidateAsync(request);

        if (!validator.IsValid)
        {
            throw new ValidationException(validator.Errors);
        }

        var TenantSetting = _mapper.Map<WE.IOTService.Domain.Entities.TenantSetting>(request);
        var result = _tenantSettingStore.Insert(TenantSetting);
        var response = _mapper.Map<TenantSettingDto>(result);
        return response;
    }

    public async Task<PagedList<TenantSettingDto>> GetAllTenantSetting(SearchTenantSettingViewModel search)
    {
        var listResult = await _tenantSettingStore.GetAll().ToPagedList(search);
        var result = _mapper.Map<PagedList<TenantSettingDto>>(listResult);
        return result;
    }

    public async Task<bool> UpdateTenantSetting(UpdateTenantSettingModel model)
    {
        var validator = await (new UpdateTenantSettingModelValidator()).ValidateAsync(model);

        if (!validator.IsValid)
        {
            throw new ValidationException(validator.Errors);
        }

        var entity = _tenantSettingStore.GetFirstOrDefault();

        if (entity != null)
        {
            entity.TariffValue = model.TariffValue;
            entity.TariffUnit = model.TariffUnit;
            entity.UpdatedDate = DateTime.UtcNow;
            _tenantSettingStore.Update(entity, x => x.Id == entity.Id);
            return true;
        }

        return false;
    }

    public async Task<bool> DeleteTenantSetting(string? id, string? key)
    {
        if (!string.IsNullOrEmpty(id))
        {
            await _tenantSettingStore.DeleteAsync(x => x.Id == id);
        }
        if (!string.IsNullOrEmpty(key))
        {
            var existed = (await _tenantSettingStore.FindAsync(x => x.Key == key)).FirstOrDefault();
            if (existed != null)
            {
                await _tenantSettingStore.DeleteAsync(x => x.Id == existed.Id);
            }
        }
        return true;
    }
    public TenantSettingDto GetTenantSetting()
    {
        var tenantSetting = _tenantSettingStore.GetAll().FirstOrDefault();
        return _mapper.Map<TenantSettingDto>(tenantSetting);
    }

    public async Task<string> GetTenantLogo()
    {
        PipelineDefinition<TenantSetting, LogoDto> pipeline = new BsonDocument[]
        {
            new BsonDocument("$match",
            new BsonDocument("$expr",
            new BsonDocument("$eq",
            new BsonArray
                        {
                            "$Key",
                            "Logo"
                        }))),
            new BsonDocument("$addFields",
            new BsonDocument("DateTime",
            new BsonDocument("$toDate", "$CreatedDate"))),
            new BsonDocument("$sort",
            new BsonDocument("DateTime", -1)),
            new BsonDocument("$project",
            new BsonDocument("Base64String", "$Value"))
        };

        var result = _tenantSettingStore.Collection.Aggregate(pipeline).ToList();

        if (result == null || result.Count == 0)
        {
            return String.Empty;
        }

        return result.FirstOrDefault().Base64String;
    }

    public async Task CreateLogoSetting(string base64String)
    {
        var existed = (await _tenantSettingStore.FindAsync(x => x.Key == TenantSettingKeyConstants.LogoSettingKey)).FirstOrDefault();

        if (existed != null)
        {

            var valueExisted = new BsonString(base64String);
            existed.Value = valueExisted;
            existed.UpdatedDate = DateTime.UtcNow;
            await _tenantSettingStore.UpdateAsync(existed, x => x.Id == existed.Id);
            return;
        }

        var valueNew = new BsonString(base64String);

        var tenantLogo = new TenantSetting()
        {
            Key = TenantSettingKeyConstants.LogoSettingKey,
            Value = valueNew,
            CreatedDate = DateTime.UtcNow,
        };

        await _tenantSettingStore.InsertAsync(tenantLogo);
    }

    public async Task<TariffValue> GetTarifSetting(DateTime? dateTime)
    {
        try
        {
            PipelineDefinition<TenantSetting, TariffValue> pipelineForTariff = new BsonDocument[]
            {
                    new BsonDocument("$match",
                    new BsonDocument("$expr",
                    new BsonDocument("$eq",
                    new BsonArray
                                {
                                    "$Key",
                                    "Tariff"
                                }))),
                    new BsonDocument("$addFields",
                    new BsonDocument("DateTime",
                    new BsonDocument("$toDate", "$CreatedDate"))),
                    new BsonDocument("$sort",
                    new BsonDocument("$DateTime", -1)),
                    new BsonDocument("$project",
                    new BsonDocument("Items", "$Value")),
                    new BsonDocument("$unwind",
                    new BsonDocument("path", "$Items")),
                    new BsonDocument("$addFields",
                    new BsonDocument("Year",
                    new BsonDocument("$substr",
                    new BsonArray
                                {
                                    "$Items.yrmonth",
                                    0,
                                    4
                                }))),
                    new BsonDocument("$addFields",
                    new BsonDocument("Month",
                    new BsonDocument("$substr",
                    new BsonArray
                                {
                                    "$Items.yrmonth",
                                    4,
                                    6
                                }))),
                    new BsonDocument("$addFields",
                    new BsonDocument("Value", "$Items.value")),
                    new BsonDocument("$addFields",
                    new BsonDocument("Unit",
                    new BsonDocument("$ifNull",
                    new BsonArray
                                {
                                    "$Items.unit",
                                    "Kwh"
                                })))
            };

            var year = dateTime.HasValue == true ? dateTime.Value.Year : -1;
            var month = dateTime.HasValue == true ? dateTime.Value.Month : -1;

            var listItem = await _tenantSettingStore.Collection.Aggregate(pipelineForTariff).ToListAsync();

            if (year == -1 || month == -1)
            {
                return listItem.Last();
            }
            else
            {
                listItem.Reverse();
                return listItem
                    .Where(x => int.Parse(x.Month) <= month && int.Parse(x.Year) == year)
                    .FirstOrDefault();
            }

        }
        catch (Exception ex)
        {
            return null;
        }

    }

    public async Task CreateSetting(CreateTariffSettingModel model ,string? tariffType)
    {

        PipelineDefinition<TenantSetting, TariffValue> pipelineForTariff = new BsonDocument[]
            {
                    new BsonDocument("$match",
                    new BsonDocument("$expr",
                    new BsonDocument("$eq",
                    new BsonArray
                                {
                                    "$Key",
                                    tariffType
                                }))),
                    new BsonDocument("$addFields",
                    new BsonDocument("DateTime",
                    new BsonDocument("$toDate", "$CreatedDate"))),
                    new BsonDocument("$sort",
                    new BsonDocument("$DateTime", -1)),
                    new BsonDocument("$project",
                    new BsonDocument("Items", "$Value")),
                    new BsonDocument("$unwind",
                    new BsonDocument("path", "$Items")),
                    new BsonDocument("$addFields",
                    new BsonDocument("Year",
                    new BsonDocument("$substr",
                    new BsonArray
                                {
                                    "$Items.yrmonth",
                                    0,
                                    4
                                }))),
                    new BsonDocument("$addFields",
                    new BsonDocument("Month",
                    new BsonDocument("$substr",
                    new BsonArray
                                {
                                    "$Items.yrmonth",
                                    4,
                                    6
                                }))),
                    new BsonDocument("$addFields",
                    new BsonDocument("Value", "$Items.value")),
                    new BsonDocument("$addFields",
                    new BsonDocument("Unit",
                    new BsonDocument("$ifNull",
                    new BsonArray
                                {
                                    "$Items.unit",
                                    "Kwh"
                                })))
            };

        var listItem = await _tenantSettingStore.Collection.Aggregate(pipelineForTariff).ToListAsync();

        if (listItem == null)
        {
            listItem = new List<TariffValue>();
        }

        var isUpdateTariff = false;

        var newTariffs = listItem.Select(x =>
        {
            var currentYear = model.yrmonth.Substring(0, 4);
            var currentMonth = model.yrmonth.Substring(4);

            if (x.Year == currentYear && x.Month == currentMonth)
            {
                isUpdateTariff = true;

                return new
                {
                    yrmonth = model.yrmonth,
                    value = model.value,
                    unit = model.unit
                };

            }
            return new
            {
                yrmonth = x.Year + x.Month,
                value = x.Value,
                unit = x.Unit
            };

        }).ToList();


        if (!isUpdateTariff)
        {
            newTariffs.Add(new
            {
                yrmonth = model.yrmonth,
                value = model.value,
                unit = model.unit
            });
        }

        BsonArray array = BsonSerializer.Deserialize<BsonArray>(JsonSerializer.Serialize(newTariffs));

        var id = listItem.Any()? listItem.FirstOrDefault().Id : string.Empty;

        var newtariffDocument = new TenantSetting()
        {
            Key = tariffType,
            CreatedDate = DateTime.UtcNow,
            Value = array
        };
        if (string.IsNullOrEmpty(id))
        {
            await _tenantSettingStore.InsertAsync(newtariffDocument);
            return;
        }
        
        newtariffDocument.UpdatedDate = DateTime.UtcNow;
        newtariffDocument.Id = id;
        await _tenantSettingStore.UpdateAsync(newtariffDocument, x => x.Id == id);
    }

    public async Task<List<TariffValue>> GetListTariffSetting(SearchTariffSettingModel model, string? tariffType)
    {
        //logic to make report still working until update
        if (string.IsNullOrEmpty(tariffType))
        {
            tariffType = "Tariff";
        }
        try
        {
            PipelineDefinition<TenantSetting, TariffValue> pipelineForTariff = new BsonDocument[]
            {
                    new BsonDocument("$match",
                    new BsonDocument("$expr",
                    new BsonDocument("$eq",
                    new BsonArray
                                {
                                    "$Key",
                                    tariffType
                                }))),
                    new BsonDocument("$addFields",
                    new BsonDocument("DateTime",
                    new BsonDocument("$toDate", "$CreatedDate"))),
                    new BsonDocument("$sort",
                    new BsonDocument("$DateTime", -1)),
                    new BsonDocument("$project",
                    new BsonDocument("Items", "$Value")),
                    new BsonDocument("$unwind",
                    new BsonDocument("path", "$Items")),
                    new BsonDocument("$addFields",
                    new BsonDocument("Year",
                    new BsonDocument("$substr",
                    new BsonArray
                                {
                                    "$Items.yrmonth",
                                    0,
                                    4
                                }))),
                    new BsonDocument("$addFields",
                    new BsonDocument("Month",
                    new BsonDocument("$substr",
                    new BsonArray
                                {
                                    "$Items.yrmonth",
                                    4,
                                    6
                                }))),
                    new BsonDocument("$addFields",
                    new BsonDocument("Value", "$Items.value")),
                    new BsonDocument("$addFields",
                    new BsonDocument("Unit",
                    new BsonDocument("$ifNull",
                    new BsonArray
                                {
                                    "$Items.unit",
                                    "Kwh"
                                })))
            };

            var year = model.TariffStartDate != null ? model.TariffStartDate.Value.Year : -1;
            var month = model.TariffStartDate != null ? model.TariffStartDate.Value.Month : -1;

            var listItem = await _tenantSettingStore.Collection.Aggregate(pipelineForTariff).ToListAsync();

            if (year == -1 || month == -1)
            {
                listItem.Reverse();
                return listItem;
            }
            else
            {
                listItem.Reverse();
                return listItem
                    .Where(x => int.Parse(x.Month) <= month && int.Parse(x.Year) == year).ToList();
            }

        }
        catch (Exception ex)
        {
            return null;
        }
    }

    public async Task<TenantSetting> CreateThresholdSetting(CreateOrUpdateThresholdSettingModel model)
    {
        var threshold = new TenantSetting()
        {
            Key = TenantSettingKeyConstants.ThresholdSettingKey,
            CreatedDate = DateTime.UtcNow,
            Value = model.ToBsonDocument(),
        };

        var result = await _tenantSettingStore.InsertAsync(threshold);

        return result;
    }
    public async Task UpdateThresholdSetting(string? id, CreateOrUpdateThresholdSettingModel model)
    {
        var items = await _tenantSettingStore.FindAsync(x => x.Id == id);
        if (items.Any())
        {
            var record = items.FirstOrDefault();
            record.Value = model.ToBsonDocument();
            record.UpdatedDate = DateTime.UtcNow;
            await _tenantSettingStore.UpdateAsync(record, x => x.Id == id);
        }
    }
    public async Task<TenantSetting> CreateAlarmRecepient(AlarmRecepient model)
    {
        var recepientlst = new TenantSetting()
        {
            Key = TenantSettingKeyConstants.AlarmRecepientKey,
            CreatedDate = DateTime.UtcNow,
            Value = model.ToBsonDocument(),
        };

        return await _tenantSettingStore.InsertAsync(recepientlst);
    }
    public async Task UpdateAlarmRecepient(string id, AlarmRecepient model)
    {
        var items = await _tenantSettingStore.FindAsync(x => x.Id == id);
        if (items.Any())
        {
            var record = items.First();
            record.Value = model.ToBsonDocument();
            record.UpdatedDate = DateTime.UtcNow;
            await _tenantSettingStore.UpdateAsync(record, x => x.Id == id);
        }
    }
    public async Task<AlarmRecepient> GetAlarmRecepient(string id)
    {
        var items = await _tenantSettingStore.FindAsync(x => x.Id == id);
        return BsonSerializer.Deserialize<AlarmRecepient>(items.FirstOrDefault().Value.ToJson());
    }

    public async Task<PagedList<AlarmRecepientDto>> SearchAlarmRecepient(AlarmRecepientSearch search)
    {
        var items = await _tenantSettingStore.FindAsync(x => x.Key == TenantSettingKeyConstants.AlarmRecepientKey);
        var result = new PagedList<AlarmRecepientDto>(items.Select(i => new AlarmRecepientDto()
        { Id = i.Id, Value = BsonSerializer.Deserialize<AlarmRecepient>(i.Value.ToBsonDocument()) }), search.PageIndex, search.PageSize, 1);
        return result;
    }

    public async Task<TenantSetting> CreateTenantApplication(TenantApplication model)
    {
        var recepientlst = new TenantSetting()
        {
            Key = TenantSettingKeyConstants.TenantApplicationKey,
            CreatedDate = DateTime.UtcNow,
            Value = model.ToBsonDocument(),
        };
        return await _tenantSettingStore.InsertAsync(recepientlst);
    }
    public async Task UpdateTenantApplication(TenantApplication model)
    {
        var existed = (await _tenantSettingStore.FindAsync(x => x.Key == TenantSettingKeyConstants.TenantApplicationKey)).FirstOrDefault();
        if (existed != null)
        {
            existed.Value = model.ToBsonDocument();
            existed.UpdatedDate = DateTime.UtcNow;
            await _tenantSettingStore.UpdateAsync(existed, x => x.Id == existed.Id);
        }
    }
    public async Task<TenantApplication> GetTenantApplication()
    {
        var items = (await _tenantSettingStore.FindAsync(x => x.Key == TenantSettingKeyConstants.TenantApplicationKey)).FirstOrDefault();
        if (items != null)
        {
            return BsonSerializer.Deserialize<TenantApplication>(items.Value.ToJson());
        }
        return new TenantApplication();
    }



    public async Task<PagedList<CreateOrUpdateThresholdSettingDTO>> GetThreshold(TenantApplicationSearch search)
    {

        var items = (await _tenantSettingStore.FindAsync(x => x.Key == TenantSettingKeyConstants.Threshold));
        //var items = await _tenantSettingStore.FindAsync(x => x.Key == TenantSettingKeyConstants.AlarmRecepientKey);

        var listItems = items.Select(i => new CreateOrUpdateThresholdSettingDTO()
        {
            Id = i.Id,
            Value = BsonSerializer.Deserialize<CreateOrUpdateThresholdSettingModel>(i.Value.ToBsonDocument())
        }).ToList();

        var result = new PagedList<CreateOrUpdateThresholdSettingDTO>(listItems.AsEnumerable(),search.PageIndex, search.PageSize, 1);

        result.Items = result.Items.Select(i => new CreateOrUpdateThresholdSettingDTO()
        {
            Id = i.Id,
            DeviceName = _deviceRepository.Find(x => x.SourceId.Equals(i.Value.Device)).FirstOrDefault()?.Name,
            Criteria = i.Value.Criteria,
            Device = i.Value.Device,
            ValueFrom = i.Value.ValueFrom,
            ValueTo = i.Value.ValueTo,
            ValueEqual = i.Value.ValueEqual,
            Operator = i.Value.Operator,
            Actions = i.Value.Actions,
        }).ToList();

        return result;

    }


    public async Task<CreateOrUpdateThresholdSettingDTO> GetThresholdByKey(string id)
    {
        try
        {
            var items = (await _tenantSettingStore.FindAsync(x => x.Id == id)).FirstOrDefault();
            if (items != null)
            {
                var results = BsonSerializer.Deserialize<CreateOrUpdateThresholdSettingDTO>(items.Value.ToJson());
                results.Id = id;
                return results;
            }
            return new CreateOrUpdateThresholdSettingDTO();
        }
        catch (Exception ex)
        {
            throw ex;
        }

    }

    public async Task<BatchDeviceDto> GetPowersSetting()
    {
        PipelineDefinition<TenantSetting, PowersDto> pipelineForTariff = new BsonDocument[]
        {
            new BsonDocument("$match",
            new BsonDocument("Key", "Power")),
            new BsonDocument("$project",
            new BsonDocument
                {
                    { "Zones", "$Value" },
                    { "All", BsonNull.Value }
                })
        };

        PipelineDefinition<Operation, Operation> pipelineOperation = new BsonDocument[]
        {
                new BsonDocument("$addFields",
                new BsonDocument("ExeTime",
                new BsonDocument("$arrayElemAt",
                new BsonArray
                            {
                                "$ExecutionTime",
                                0
                            }))),
                new BsonDocument("$match",
                new BsonDocument("$or",
                new BsonArray
                        {
                            new BsonDocument("CommandStatus",
                            new BsonDocument("$eq", "Failed")),
                            new BsonDocument("CommandStatus",
                            new BsonDocument("$eq", "Processing")),
                            new BsonDocument("CommandStatus",
                            new BsonDocument("$eq", "Processed"))
                        })),
                new BsonDocument("$sort",
                new BsonDocument("ExeTime", 1)),
                new BsonDocument("$project",
                 new BsonDocument
                    {
                        { "Command", "$Command" },
                        { "Source", "$Source" },
                        { "CNO", "$CNO" },
                        { "CommandStatus", "$CommandStatus" },
                        { "ExecutionTime", "$ExecutionTime" },
                        { "PointStatus", "$PointStatus" },
                        { "Point", "$Point" },
                        { "Type", "$Type" },
                        { "PinNumber", "$PinNumber" },
                        { "CommandExecutionMesssage", "$CommandExecutionMesssage" }
                    })
        };

        var lsitOperation = await _operationRespository.ExecuteAggregateAsync<Operation>(pipelineOperation, "Source");

        var operationMapped = lsitOperation.Select(x => new OperationDto()
        {
            Command = x.Command,
            Source = x.Source,
            CNO = x.CNO,
            CommandStatus = ConvertOperationStatus(x.CommandStatus),
            ExecutionTime = x.ExecutionTime,
            PointStatus = x.PointStatus,
            Point = x.Point,
        }).ToList();

        var powersDto = _tenantSettingStore.Collection.Aggregate<PowersDto>(pipelineForTariff).ToList().FirstOrDefault();

        var allDeviceIds = _deviceRepository.GetAll().Select(x => x.SourceId).ToList();

        var allZone = _deviceRepository.GetAll().ToList().Select(x => new PowerDeviceDto()
        {
            DeviceName = x.Name,
            DeviceId = x.SourceId,
            Status = x.OperationStatus,
            Commands = operationMapped.Where(y => y.Source == x.SourceId).ToList()
        }).ToList();


        var zones = powersDto.Zones.Select(x => new ZoneDto()
        {
            ZoneName = x.Name,
            Devices = allZone.Where(z => x.DeviceIds.Contains(z.DeviceId)).ToList(),
            
        });

        var result = new BatchDeviceDto()
        {
            All = new ZoneDto()
            {
                ZoneName = "All",
                Devices = allZone
            },
            Zones = zones.ToList()
        };

        return result;
    }

    private OperationStatus ConvertOperationStatus(string status)
    {

        switch (status)

        {
            case OperationConstants.OperationProcessing:
                {
                    return OperationStatus.Processing;
                }
            case OperationConstants.OperationProcessed:
                {
                    return OperationStatus.Processed;
                }
            case OperationConstants.OperationCanceled:
                {
                    return OperationStatus.Canceled;
                }
            default:
                {
                    return OperationStatus.Failed;
                }
        }
    }

    public async Task<TenantSetting> CreateChartStyleSetting(ChartStyleSetting model)
    {
        var recepientlst = new TenantSetting()
        {
            Key = TenantSettingKeyConstants.ChartStyleSettingKey,
            CreatedDate = DateTime.UtcNow,
            Value = model.ToBsonDocument(),
        };
        return await _tenantSettingStore.InsertAsync(recepientlst);
    }
    public async Task UpdateChartStyleSetting(ChartStyleSetting model, string id)
    {
        var existed = (await _tenantSettingStore.FindAsync(x => x.Id == id)).FirstOrDefault();
        if (existed != null)
        {
            existed.Value = model.ToBsonDocument();
            existed.UpdatedDate = DateTime.UtcNow;
            await _tenantSettingStore.UpdateAsync(existed, x => x.Id == existed.Id);
        }
    }

    public async Task<List<ChartStyleSettingDto>> GetChartStyleSetting(string? chartTypeName)
    {
        var items = (await _tenantSettingStore.FindAsync(x => x.Key == TenantSettingKeyConstants.ChartStyleSettingKey)).Select(x => new ChartStyleSettingDto() { Id = x.Id, ChartStyleSetting = BsonSerializer.Deserialize<ChartStyleSetting>(x.Value.ToJson()) });
        if (!string.IsNullOrEmpty(chartTypeName))
        {
            return items.Where(x => x.ChartStyleSetting != null && x.ChartStyleSetting.ChartTypeName.Equals(chartTypeName)).ToList();
        }
        return items.ToList();
    }
    public async Task<ChartStyleSettingDto> GetChartStyleSettingById(string? id)
    {
        var result = (await _tenantSettingStore.FindAsync(x => x.Id == id)).Select(x => new ChartStyleSettingDto() { Id = x.Id, ChartStyleSetting = BsonSerializer.Deserialize<ChartStyleSetting>(x.Value.ToJson()) });
        return result.FirstOrDefault();
    }

    public async Task CreatePowersSetting(CreatePowerBatchModel model)
    {
        PipelineDefinition<TenantSetting, PowersDto> pipelineForTariff = new BsonDocument[]
       {
            new BsonDocument("$match",
            new BsonDocument("Key", "Power")),
            new BsonDocument("$project",
            new BsonDocument
                {
                    { "Zones", "$Value" },
                    { "All", BsonNull.Value }
                })
       };

        var powersDto = _tenantSettingStore.Collection.Aggregate<PowersDto>(pipelineForTariff).ToList().FirstOrDefault();

        BsonArray bArray = new BsonArray();
        foreach (var term in model.Zones)
        {
            bArray.Add(term.ToBsonDocument());
        }

        var newPower = new TenantSetting()
        {
            Key = TenantSettingKeyConstants.PowerKey,
            Value = bArray,
            CreatedDate = DateTime.UtcNow
        };

        if (powersDto != null)
        {
            await _tenantSettingStore.DeleteAsync(x => x.Id == powersDto.Id);
        }

        await _tenantSettingStore.InsertAsync(newPower);    
    }

    public async Task<TenantSetting> CreateLeftMenuSetting(LeftMenuSetting model)
    {
        var settinglst = new TenantSetting()
        {
            Key = TenantSettingKeyConstants.FeatureSettingKey,
            CreatedDate = DateTime.UtcNow,
            Value = model.ToBsonDocument(),
        };
        return await _tenantSettingStore.InsertAsync(settinglst);
    }
    public async Task UpdateLeftMenuSetting(LeftMenuSetting model)
    {
        var existed = (await _tenantSettingStore.FindAsync(x => x.Key == TenantSettingKeyConstants.FeatureSettingKey)).FirstOrDefault();
        if (existed != null)
        {
            existed.Value = model.ToBsonDocument();
            existed.UpdatedDate = DateTime.UtcNow;
            await _tenantSettingStore.UpdateAsync(existed, x => x.Id == existed.Id);
        }
    }

    public async Task<LeftMenuSetting> GetLeftMenuSetting()
    {   
        var items = (await _tenantSettingStore.FindAsync(x => x.Key == TenantSettingKeyConstants.FeatureSettingKey)).Select(x => BsonSerializer.Deserialize<LeftMenuSetting>(x.Value.ToJson()));
        if (items.Any())
        {
            return items.First();
        }
        return new LeftMenuSetting();
    }

}
