using AutoMapper;
using Common.RestClientHelpers;
using MongoDB.Bson;
using MongoDB.Driver;
using WE.IOT.Service.Application.Constants;
using WE.IOT.Service.Application.Dtos.Operation;
using WE.IOT.Service.Application.Models.Operation;
using WE.IOTService.Domain.Store;
using WE.IOT.Service.Domain.Entities;
using WE.IOT.Service.Domain.Repository;

namespace WE.IOT.Service.Application.Services;

public interface IOperationService
{
    public Task<bool> CreateTurnOffDevice(string deviceId, string command, CreateTurnOffModel model);
    public Task<bool> CreateTurnOnDevice(string deviceId, string command, CreateTurnOffModel model);

    public Task<ListOperationDto> GetOperation(string? deviceId);
}

public class OperationService : IOperationService
{
    private readonly IOperationRepository _operationRespository;
    private readonly IMapper _mapper;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceStatusService _deviceStatusService;
    private readonly TurnOnOffDeviceClient _turnOnOffDeviceClient;

    public OperationService(
        IOperationRepository operationRespository,
        IMapper mapper,
        IDeviceRepository deviceRepository,
        IDeviceStatusService deviceStatusService,
        TurnOnOffDeviceClient turnOnOffDeviceClient
        )
    {
        this._operationRespository = operationRespository ?? throw new ArgumentNullException(nameof(operationRespository));
        this._mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        this._deviceRepository = deviceRepository ?? throw new ArgumentNullException(nameof(deviceRepository));
        this._deviceStatusService = deviceStatusService ?? throw new ArgumentNullException(nameof(deviceStatusService));
        this._turnOnOffDeviceClient = turnOnOffDeviceClient ?? throw new ArgumentException(nameof(turnOnOffDeviceClient));
    }


    public async Task<bool> CreateTurnOffDevice(string deviceId, string command, CreateTurnOffModel model)
    {
        try
        {
            var turnOff = new Operation()
            {
                CNO = Guid.NewGuid().ToString(),
                CreatedDate = DateTime.UtcNow,
                ExecutionTime = model.StartDate,
                Source = deviceId,
                Command = OperationConstants.TurnOff,
                Point = model.Point,
                PointStatus = model.PointStatus,
                Type = OperationConstants.DeviceOperationType,
                CommandStatus = OperationConstants.OperationProcessing
            };

            var turnOn = new Operation()
            {
                CNO = Guid.NewGuid().ToString(),
                CreatedDate = DateTime.UtcNow,
                ExecutionTime = model.EndDate,
                Source = deviceId,
                Command = OperationConstants.TurnOn,
                Point = model.Point,
                PointStatus = model.PointStatus,
                Type = OperationConstants.DeviceOperationType,
                CommandStatus = OperationConstants.OperationProcessing
            };

            await _operationRespository.InsertAsync(turnOff);
            await _operationRespository.InsertAsync(turnOn);
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    public async Task<bool> CreateTurnOnDevice(string deviceId, string command, CreateTurnOffModel model)
    {
        try
        {
            var turnOn = new Operation()
            {
                CNO = Guid.NewGuid().ToString(),
                CreatedDate = DateTime.UtcNow,
                ExecutionTime = model.StartDate,
                Source = deviceId,
                Command = OperationConstants.TurnOn,
                Point = model.Point,
                PointStatus = model.PointStatus,
                Type = OperationConstants.DeviceOperationType,
                CommandStatus = OperationConstants.OperationProcessing
            };

            var turnOff = new Operation()
            {
                CNO = Guid.NewGuid().ToString(),
                CreatedDate = DateTime.UtcNow,
                ExecutionTime = model.EndDate,
                Source = deviceId,
                Command = OperationConstants.TurnOff,
                Point = model.Point,
                PointStatus = model.PointStatus,
                Type = OperationConstants.DeviceOperationType,
                CommandStatus = OperationConstants.OperationProcessing
            };

            await _operationRespository.InsertAsync(turnOff);
            await _operationRespository.InsertAsync(turnOn);

            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    public async Task<ListOperationDto> GetOperation(string? deviceId)
    {
        var currentTime = DateTimeOffset.UtcNow.Ticks;

        PipelineDefinition<Operation, Operation> pipeline = new BsonDocument[]
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

        if (!string.IsNullOrEmpty(deviceId))
        {
            pipeline = new BsonDocument[]
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
                new BsonDocument("$and",
                new BsonArray
                        {
                            new BsonDocument("Source",
                            new BsonDocument("$eq", deviceId))
                        })),
                new BsonDocument("$match",
                new BsonDocument("$or",
                new BsonArray
                        {
                            new BsonDocument("CommandStatus",
                            new BsonDocument("$eq", "Error")),
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
        }

        var all = (await _operationRespository.ExecuteAggregateAsync<Operation>(pipeline, "Source")).ToList();
        var devices = _deviceRepository.GetAll().ToList();

        var mapped = all.Select(x => new OperationDto()
        {
            Command = x.Command,
            Source = x.Source,
            CNO = x.CNO,
            CommandStatus = ConvertOperationStatus(x.CommandStatus),
            ExecutionTime = x.ExecutionTime,
            PointStatus = x.PointStatus,
            Point = x.Point,
            DeviceName = devices.Where(y => x.Source == y.SourceId).FirstOrDefault().Name
        }).ToList();

        return new ListOperationDto()
        {
            gt_DeviceCommands = mapped
        };
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


}

