using Common.ControllerBase;
using Common.Mongodb.MultiTenant;
using Common.Mongodb.Repository;
using Common.MongoDb.Repository;
using Common.MongoDbMultitenant;
using Common.RestClientHelpers;
using DocumentFormat.OpenXml.EMMA;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Quartz;
using WE.IOT.Service.Application.Constants;
using WE.IOT.Service.Application.Dtos.Operation;
using WE.IOT.Service.Application.Models.Operation;
using WE.IOT.Service.Application.Services;
using WE.IOTService.Domain.Entities;

namespace WE.IOT.Service.Controllers;

[ApiController]
[Route("[controller]")]
public class OperationController : ApiControllerBase
{
    private readonly IOperationService _operationService;
    private readonly IDeviceService _deviceService;
    private readonly IDeviceStatusService _deviceStatusService;
    private readonly TurnOnOffDeviceClient _turnOnOffDeviceClient;
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IMultiTenantService<Tenant> _multiTenantService;

    public OperationController(IOperationService operationService,
        IDeviceService deviceService,
        IDeviceStatusService deviceStatusService,
        TurnOnOffDeviceClient turnOnOffDeviceClient,
        IRepositoryFactory repositoryFactory,
        ISchedulerFactory schedulerFactory,
        IMultiTenantService<Tenant> multiTenantService)
    {
        this._operationService = operationService ?? throw new ArgumentNullException(nameof(operationService));
        this._deviceService = deviceService ?? throw new ArgumentNullException(nameof(deviceService));
        this._deviceStatusService = deviceStatusService ?? throw new ArgumentNullException(nameof(deviceStatusService));
        this._turnOnOffDeviceClient = turnOnOffDeviceClient ?? throw new ArgumentNullException(nameof(turnOnOffDeviceClient));
        this._repositoryFactory = repositoryFactory ?? throw new ArgumentNullException(nameof(repositoryFactory));
        this._schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
        this._multiTenantService = multiTenantService ?? throw new ArgumentNullException(nameof(multiTenantService));
    }

    [HttpGet("cancel/{cno}")]
    [Produces(typeof(ListOperationDto))]
    public async Task<IActionResult> CancelCno(string cno)
    {
        var currentCm = await _repositoryFactory.GetRepository<WE.IOT.Service.Domain.Entities.Operation>("Operation").FindAsync(x => x.CNO == cno);

        if (currentCm != null && currentCm.Any())
        {
            var first = currentCm.First();

            var currentDevice = _repositoryFactory.GetRepository<WE.IOTService.Domain.Entities.Device>("Device").GetFirstOrDefault(x => x.SourceId == first.Source && x.Status == "Active");

            if (currentDevice == null) return BadRequest("Device not found or inactive!"); ;

            if (first.CommandStatus == OperationConstants.OperationProcessed)
            {
                var previous = first.Point == OperationConstants.StatusOn ? (int)DevicePowerStatus.On : (int)DevicePowerStatus.Off;
                var currentStatus = first.PointStatus;
                var deviceId = first.Source;
                var pinNumber = first.PinNumber;

                try
                {
                    var result = await _turnOnOffDeviceClient.CreatePowerCommand(deviceId, pinNumber, previous);

                    if (result.success)
                    {
                        first.CommandStatus = OperationConstants.OperationCanceled;

                        await _repositoryFactory.GetRepository<WE.IOT.Service.Domain.Entities.Operation>("Operation").UpdateAsync(first, x => x.CNO == cno);

                        return Ok();
                    }
                    else
                    {
                        first.CommandStatus = OperationConstants.OperationCanceled;
                        first.UpdatedDate = DateTime.UtcNow;

                        await _repositoryFactory.GetRepository<WE.IOT.Service.Domain.Entities.Operation>("Operation").UpdateAsync(first, x => x.CNO == cno);

                        return Ok("Gtech command have canced but there was an error while executing third party api with section id: " + result.sessionId + ". Cannot revoke executed command!");
                    }
                }
                catch (Exception ex)
                {
                    first.CommandStatus = OperationConstants.OperationCanceled;
                    first.UpdatedDate = DateTime.UtcNow;
                    await _repositoryFactory.GetRepository<WE.IOT.Service.Domain.Entities.Operation>("Operation").UpdateAsync(first, x => x.CNO == cno);

                    return Ok("Gtech command have canced but there was an error while executing third party api: " + ex.Message + ". Cannot revoke executed command!");
                }
            }
            else
            {
                first.CommandStatus = OperationConstants.OperationCanceled;
                first.UpdatedDate = DateTime.UtcNow;

                await _repositoryFactory.GetRepository<WE.IOT.Service.Domain.Entities.Operation>("Operation").UpdateAsync(first, x => x.CNO == cno);
                
                return Ok("Gtech command " + first.CNO + " have canceled!");
            }
        }
        else
        {
            return BadRequest("Comand not found!");
        }
    }

    [HttpPost("batch")]
    public async Task<IActionResult> DevicesPower([FromBody] List<DevicesPowerModel> model)
    {

        if (model.IsNullOrEmpty())
        {
            return NoContent();
        }
        var isIssue = string.Empty;
        var sucessMessage = string.Empty;

        foreach (var device in model)
        {
            var currentDevice = _repositoryFactory.GetRepository<WE.IOTService.Domain.Entities.Device>("Device").GetFirstOrDefault(x => x.SourceId == device.DeviceId && x.Status == "Active");
            // validate Time

            if (currentDevice != null)
            {
                if (device.StartDate == null && device.EndDate == null)

                {
                    var operation = new WE.IOT.Service.Domain.Entities.Operation()
                    {

                        CNO = Guid.NewGuid().ToString(),
                        CreatedDate = DateTime.UtcNow,
                        ExecutionTime = DateTimeOffset.UtcNow,
                        Source = device.DeviceId,
                        Command = device.PinState == DevicePowerStatus.On ? OperationConstants.TurnOn : OperationConstants.TurnOff,
                        Point = currentDevice.OperationStatus,
                        Type = OperationConstants.DeviceOperationType,
                        CommandStatus = OperationConstants.OperationProcessing,
                        PinNumber = device.PinNumber,
                    };

                    try
                    {
                        var result = await _turnOnOffDeviceClient.CreatePowerCommand(device.DeviceId, device.PinNumber, (int)device.PinState);

                        if (result.success)
                        {
                            currentDevice.OperationStatus = device.PinState == DevicePowerStatus.On ? "On" : "Off";

                            operation.CommandStatus = OperationConstants.OperationProcessed;
                            operation.UpdatedDate = DateTime.UtcNow;
                            operation.PointStatus = device.PinState == DevicePowerStatus.On ? "On" : "Off";

                            await _repositoryFactory.GetRepository<WE.IOT.Service.Domain.Entities.Operation>("Operation").InsertAsync(operation);
                            await _repositoryFactory.GetRepository<WE.IOTService.Domain.Entities.Device>("Device").UpdateAsync(currentDevice, x => x.Id == currentDevice.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        operation.CommandStatus = OperationConstants.OperationFailed;
                        operation.UpdatedDate = DateTime.UtcNow;
                        operation.CommandExecutionMesssage = ex.Message;

                        await _repositoryFactory.GetRepository<WE.IOT.Service.Domain.Entities.Operation>("Operation").InsertAsync(operation);

                        isIssue += "Gtech command: " + operation.CNO + "just have created but" + " have issue when call third party api: " + ex.Message + ". This command will be auto retry leter! \n";

                    }
                }
                else if (device.StartDate != null && device.EndDate != null)
                {
                    {
                        var currentTime = DateTimeOffset.UtcNow;

                        if (device.StartDate.Value <= currentTime)
                        {
                            return BadRequest("FromDate need to greater than or equal now!");
                        }

                        if (device.EndDate.Value < currentTime)
                        {
                            return BadRequest("EndDate need to greater than now!");
                        }


                        var firstOperation = new WE.IOT.Service.Domain.Entities.Operation()
                        {
                            CNO = Guid.NewGuid().ToString(),
                            CreatedDate = DateTime.UtcNow,
                            ExecutionTime = device.StartDate.Value,
                            Source = device.DeviceId,
                            Point = currentDevice.OperationStatus,
                            Command = device.PinState == DevicePowerStatus.On ? OperationConstants.TurnOn : OperationConstants.TurnOff,

                            Type = OperationConstants.DeviceOperationType,
                            PinNumber = device.PinNumber,
                            CommandStatus = OperationConstants.OperationProcessing
                        };

                        var secondOperation = new WE.IOT.Service.Domain.Entities.Operation()
                        {
                            CNO = Guid.NewGuid().ToString(),
                            CreatedDate = DateTime.UtcNow,
                            ExecutionTime = device.EndDate.Value,
                            Source = device.DeviceId,
                            Point = currentDevice.OperationStatus,
                            Command = device.PinState == DevicePowerStatus.On ? OperationConstants.TurnOff : OperationConstants.TurnOn,
                            Type = OperationConstants.DeviceOperationType,
                            PinNumber = device.PinNumber,
                            CommandStatus = OperationConstants.OperationProcessing
                        };

                        if (device.StartDate <= currentTime)
                        {
                            try
                            {
                                currentDevice = _repositoryFactory.GetRepository<WE.IOTService.Domain.Entities.Device>("Device").GetFirstOrDefault(x => x.SourceId == device.DeviceId && x.Status == "Active");

                                var result = await _turnOnOffDeviceClient.CreatePowerCommand(device.DeviceId, device.PinNumber, (int)device.PinState);

                                if (result.success)
                                {
                                    firstOperation.UpdatedDate = DateTime.UtcNow;
                                    firstOperation.CommandStatus = OperationConstants.OperationProcessed;
                                    firstOperation.PointStatus = firstOperation.Command == OperationConstants.TurnOn ? OperationConstants.StatusOn : OperationConstants.StatusOff;

                                    currentDevice.OperationStatus = firstOperation.Command == OperationConstants.TurnOn ? OperationConstants.StatusOn : OperationConstants.StatusOff; ;

                                    await _repositoryFactory.GetRepository<WE.IOT.Service.Domain.Entities.Operation>("Operation").InsertAsync(firstOperation);
                                    await _repositoryFactory.GetRepository<WE.IOTService.Domain.Entities.Device>("Device").UpdateAsync(currentDevice, x => x.Id == currentDevice.Id);
                                }
                            }
                            catch (Exception ex)
                            {
                                firstOperation.UpdatedDate = DateTime.UtcNow;
                                firstOperation.CommandStatus = OperationConstants.OperationFailed;
                                firstOperation.CommandExecutionMesssage = ex.Message;

                                await _repositoryFactory.GetRepository<WE.IOT.Service.Domain.Entities.Operation>("Operation").InsertAsync(firstOperation);

                                isIssue += "Gtech command: " + firstOperation.Command + " have been created but" + " have issue when call third party api: " + ex.Message + ". This command will be auto retry leter! \n";
                            }
                        }
                        else
                        {
                            sucessMessage += "Gtech command: " + firstOperation.Command + " have been created. This command will be execute at " + device.StartDate + "\n";
                            await _repositoryFactory.GetRepository<WE.IOT.Service.Domain.Entities.Operation>("Operation").InsertAsync(firstOperation);
                        }

                        if (device.EndDate <= currentTime)
                        {
                            try
                            {
                                currentDevice = _repositoryFactory.GetRepository<WE.IOTService.Domain.Entities.Device>("Device").GetFirstOrDefault(x => x.SourceId == device.DeviceId && x.Status == "Active");

                                var result = await _turnOnOffDeviceClient.CreatePowerCommand(device.DeviceId, device.PinNumber, device.PinState == DevicePowerStatus.On ? (int)DevicePowerStatus.Off : (int)DevicePowerStatus.On);

                                if (result.success)
                                {
                                    secondOperation.UpdatedDate = DateTime.UtcNow;
                                    secondOperation.CommandStatus = OperationConstants.OperationProcessed;
                                    secondOperation.Point = currentDevice.OperationStatus;
                                    secondOperation.PointStatus = secondOperation.Command == OperationConstants.TurnOn ? OperationConstants.StatusOff : OperationConstants.StatusOn;

                                    currentDevice.OperationStatus = secondOperation.Command == OperationConstants.TurnOn ? OperationConstants.StatusOff : OperationConstants.StatusOn;

                                    await _repositoryFactory.GetRepository<WE.IOT.Service.Domain.Entities.Operation>("Operation").InsertAsync(firstOperation);
                                    await _repositoryFactory.GetRepository<WE.IOTService.Domain.Entities.Device>("Device").UpdateAsync(currentDevice, x => x.Id == currentDevice.Id);
                                }
                            }
                            catch (Exception ex)
                            {
                                secondOperation.UpdatedDate = DateTime.UtcNow;
                                secondOperation.CommandStatus = OperationConstants.OperationFailed;
                                secondOperation.CommandExecutionMesssage = ex.Message;

                                await _repositoryFactory.GetRepository<WE.IOT.Service.Domain.Entities.Operation>("Operation").InsertAsync(secondOperation);

                                isIssue += "Gtech command: " + secondOperation.CNO + " just have created but" + " have issue when call third party api: " + ex.Message + ". This command will be retry leter!" + "\n";
                            }
                        }
                        else
                        {
                            sucessMessage += "Gtech command: " + secondOperation.Command + " have been created. This command will be execute at " + device.EndDate + "\n";
                            await _repositoryFactory.GetRepository<WE.IOT.Service.Domain.Entities.Operation>("Operation").InsertAsync(secondOperation);
                        }
                    }

                    if ((device.StartDate == null && device.EndDate != null) || (device.StartDate != null && device.EndDate == null))
                    {
                        return BadRequest("Need submit Start Date and End Date!");
                    }
                }
                else
                {
                    return BadRequest("Device not found or inactive!");
                }
            }
            
        }
        return Ok(isIssue != string.Empty ? isIssue : sucessMessage);
    }


    [HttpPost("{device}")]
    public async Task<IActionResult> DevicePower(string device, [FromBody] DevicePowerModel model)
    {
        var currentDevice = _repositoryFactory.GetRepository<WE.IOTService.Domain.Entities.Device>("Device").GetFirstOrDefault(x => x.SourceId == device && x.Status == "Active");
        var isIssue = string.Empty;
        var sucessMessage = string.Empty;
        // validate Time

        if (currentDevice != null)
        {
            if (model.StartDate == null && model.EndDate == null)
            {
                var operation = new WE.IOT.Service.Domain.Entities.Operation()
                {

                    CNO = Guid.NewGuid().ToString(),
                    CreatedDate = DateTime.UtcNow,
                    ExecutionTime = DateTimeOffset.UtcNow,
                    Source = device,
                    Command = model.PinState == DevicePowerStatus.On ? OperationConstants.TurnOn : OperationConstants.TurnOff,
                    Point = currentDevice.OperationStatus,
                    Type = OperationConstants.DeviceOperationType,
                    CommandStatus = OperationConstants.OperationProcessing,
                    PinNumber = model.PinNumber,
                };

                try
                {
                    var result = await _turnOnOffDeviceClient.CreatePowerCommand(device, model.PinNumber, (int)model.PinState);

                    if (result.success)
                    {
                        currentDevice.OperationStatus = model.PinState == DevicePowerStatus.On ? "On" : "Off";

                        operation.CommandStatus = OperationConstants.OperationProcessed;
                        operation.UpdatedDate = DateTime.UtcNow;
                        operation.PointStatus = model.PinState == DevicePowerStatus.On ? "On" : "Off";

                        await _repositoryFactory.GetRepository<WE.IOT.Service.Domain.Entities.Operation>("Operation").InsertAsync(operation);
                        await _repositoryFactory.GetRepository<WE.IOTService.Domain.Entities.Device>("Device").UpdateAsync(currentDevice, x => x.Id == currentDevice.Id);
                    }
                }
                catch (Exception ex)
                {
                    operation.CommandStatus = OperationConstants.OperationFailed;
                    operation.UpdatedDate = DateTime.UtcNow;
                    operation.CommandExecutionMesssage = ex.Message;

                    await _repositoryFactory.GetRepository<WE.IOT.Service.Domain.Entities.Operation>("Operation").InsertAsync(operation);

                    isIssue += "Gtech command: " + operation.CNO + "just have created but" + " have issue when call third party api: " + ex.Message + ". This command will be auto retry leter! \n";

                }
            }
            else if (model.StartDate != null && model.EndDate != null)
            {
                {
                    var currentTime = DateTimeOffset.UtcNow;

                    if (model.StartDate.Value <= currentTime)
                    {
                        return BadRequest("FromDate need to greater than or equal now!");
                    }

                    if (model.EndDate.Value < currentTime)
                    {
                        return BadRequest("EndDate need to greater than now!");
                    }


                    var firstOperation = new WE.IOT.Service.Domain.Entities.Operation()
                    {
                        CNO = Guid.NewGuid().ToString(),
                        CreatedDate = DateTime.UtcNow,
                        ExecutionTime = model.StartDate.Value,
                        Source = device,
                        Point = currentDevice.OperationStatus,
                        Command = model.PinState == DevicePowerStatus.On ? OperationConstants.TurnOn : OperationConstants.TurnOff,

                        Type = OperationConstants.DeviceOperationType,
                        PinNumber = model.PinNumber,
                        CommandStatus = OperationConstants.OperationProcessing
                    };

                    var secondOperation = new WE.IOT.Service.Domain.Entities.Operation()
                    {
                        CNO = Guid.NewGuid().ToString(),
                        CreatedDate = DateTime.UtcNow,
                        ExecutionTime = model.EndDate.Value,
                        Source = device,
                        Point = currentDevice.OperationStatus,
                        Command = model.PinState == DevicePowerStatus.On ? OperationConstants.TurnOff : OperationConstants.TurnOn,
                        Type = OperationConstants.DeviceOperationType,
                        PinNumber = model.PinNumber,
                        CommandStatus = OperationConstants.OperationProcessing
                    };

                    if (model.StartDate <= currentTime)
                    {
                        try
                        {
                            currentDevice = _repositoryFactory.GetRepository<WE.IOTService.Domain.Entities.Device>("Device").GetFirstOrDefault(x => x.SourceId == device && x.Status == "Active");

                            var result = await _turnOnOffDeviceClient.CreatePowerCommand(device, model.PinNumber, (int)model.PinState);

                            if (result.success)
                            {
                                firstOperation.UpdatedDate = DateTime.UtcNow;
                                firstOperation.CommandStatus = OperationConstants.OperationProcessed;
                                firstOperation.PointStatus = firstOperation.Command == OperationConstants.TurnOn ? OperationConstants.StatusOn : OperationConstants.StatusOff;

                                currentDevice.OperationStatus = firstOperation.Command == OperationConstants.TurnOn ? OperationConstants.StatusOn : OperationConstants.StatusOff; ;

                                await _repositoryFactory.GetRepository<WE.IOT.Service.Domain.Entities.Operation>("Operation").InsertAsync(firstOperation);
                                await _repositoryFactory.GetRepository<WE.IOTService.Domain.Entities.Device>("Device").UpdateAsync(currentDevice, x => x.Id == currentDevice.Id);
                            }
                        }
                        catch (Exception ex)
                        {
                            firstOperation.UpdatedDate = DateTime.UtcNow;
                            firstOperation.CommandStatus = OperationConstants.OperationFailed;
                            firstOperation.CommandExecutionMesssage = ex.Message;

                            await _repositoryFactory.GetRepository<WE.IOT.Service.Domain.Entities.Operation>("Operation").InsertAsync(firstOperation);

                            isIssue += "Gtech command: " + firstOperation.Command + " have been created but" + " have issue when call third party api: " + ex.Message + ". This command will be auto retry leter! \n";
                        }
                    }
                    else
                    {
                        sucessMessage += "Gtech command: " + firstOperation.Command + " have been created. This command will be execute at " + model.StartDate + "\n";
                        await _repositoryFactory.GetRepository<WE.IOT.Service.Domain.Entities.Operation>("Operation").InsertAsync(firstOperation);
                    }

                    if (model.EndDate <= currentTime)
                    {
                        try
                        {
                            currentDevice = _repositoryFactory.GetRepository<WE.IOTService.Domain.Entities.Device>("Device").GetFirstOrDefault(x => x.SourceId == device && x.Status == "Active");

                            var result = await _turnOnOffDeviceClient.CreatePowerCommand(device, model.PinNumber, model.PinState == DevicePowerStatus.On ? (int)DevicePowerStatus.Off : (int)DevicePowerStatus.On);

                            if (result.success)
                            {
                                secondOperation.UpdatedDate = DateTime.UtcNow;
                                secondOperation.CommandStatus = OperationConstants.OperationProcessed;
                                secondOperation.Point = currentDevice.OperationStatus;
                                secondOperation.PointStatus = secondOperation.Command == OperationConstants.TurnOn ? OperationConstants.StatusOff : OperationConstants.StatusOn;

                                currentDevice.OperationStatus = secondOperation.Command == OperationConstants.TurnOn ? OperationConstants.StatusOff : OperationConstants.StatusOn;

                                await _repositoryFactory.GetRepository<WE.IOT.Service.Domain.Entities.Operation>("Operation").InsertAsync(firstOperation);
                                await _repositoryFactory.GetRepository<WE.IOTService.Domain.Entities.Device>("Device").UpdateAsync(currentDevice, x => x.Id == currentDevice.Id);
                            }
                        }
                        catch (Exception ex)
                        {
                            secondOperation.UpdatedDate = DateTime.UtcNow;
                            secondOperation.CommandStatus = OperationConstants.OperationFailed;
                            secondOperation.CommandExecutionMesssage = ex.Message;

                            await _repositoryFactory.GetRepository<WE.IOT.Service.Domain.Entities.Operation>("Operation").InsertAsync(secondOperation);

                            isIssue += "Gtech command: " + secondOperation.CNO + " just have created but" + " have issue when call third party api: " + ex.Message + ". This command will be retry leter!" + "\n";
                        }
                    }
                    else
                    {
                        sucessMessage += "Gtech command: " + secondOperation.Command + " have been created. This command will be execute at " + model.EndDate + "\n";
                        await _repositoryFactory.GetRepository<WE.IOT.Service.Domain.Entities.Operation>("Operation").InsertAsync(secondOperation);
                    }
                }

                if ((model.StartDate == null && model.EndDate != null) || (model.StartDate != null && model.EndDate == null))
                {
                    return BadRequest("Need submit Start Date and End Date!");
                }
            }
            else
            {
                return BadRequest("Device not found or inactive!");
            }

            return Ok(isIssue != string.Empty ? isIssue : sucessMessage);
        }
         return Ok(isIssue != string.Empty ? isIssue : sucessMessage);
    }

    private class TurnOnOffJob : IJob
    {
        private IServiceScopeFactory _serviceScopeFactory;

        public TurnOnOffJob(IServiceScopeFactory serviceScopeFactory)
        {
            this._serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        }

        public async Task Execute(IJobExecutionContext context)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var iMongodbClient = scope.ServiceProvider.GetRequiredService<IMongoClient>();
                var _turnOnOffDeviceClient = scope.ServiceProvider.GetRequiredService<TurnOnOffDeviceClient>();

                JobDataMap dataMap = context.JobDetail.JobDataMap;

                var databaseName = dataMap.GetString("DatabaseName");
                var pinState = dataMap.GetString("PinState");
                var pinNumber = dataMap.GetString("PinNumber");
                var deviceId = dataMap.GetString("DeviceId");

                var deviceRepository = new Repository<WE.IOTService.Domain.Entities.Device>(databaseName, "Device", iMongodbClient);


                var all = deviceRepository.GetAll().ToList();

                var currentDevice = (await deviceRepository.FindAsyncWithoutDataAuthorize(x => x.SourceId == deviceId && x.Status == "Active")).FirstOrDefault();

                if (pinNumber != null && pinState != null && deviceId != null)
                {
                    var result = await _turnOnOffDeviceClient.CreatePowerCommand(deviceId, pinNumber, pinState == "0" ? 0 : 1);

                    if (result.success)
                    {
                        currentDevice.OperationStatus = pinState == "On" ? "On" : "Off";
                        await deviceRepository.UpdateAsync(currentDevice, x => x.Id == currentDevice.Id, default(CancellationToken));
                    }
                }
            }
        }
    }

    [HttpPost]
    [Obsolete("change requirement for turn on turn off, backlog: 13220")]
    [Route("{device}/{command}")]
    public async Task<IActionResult> CreateTurnOnOrTurnOffCommand(string device, string command, [FromBody] CreateTurnOffModel model)
    {
        if (!(await IsActiveDevice(device)))
        {
            return BadRequest("Device is inactive or notfound!");
        }

        var result = false;

        switch (command)
        {
            case OperationConstants.TurnOn:
                {
                    result = await _operationService.CreateTurnOnDevice(device, OperationConstants.TurnOn, model);
                    break;
                }
            case OperationConstants.TurnOff:
                {
                    result = await _operationService.CreateTurnOffDevice(device, OperationConstants.TurnOn, model);
                    break;
                }
            default:
                {
                    return BadRequest();
                }
        }

        if (result) return Ok();

        return BadRequest();
    }

    [HttpGet("{id?}")]
    [Produces(typeof(ListOperationDto))]
    public async Task<IActionResult> Get(string? id)
    {
        var result = await _operationService.GetOperation(id);
        return Ok(result);
    }

    [Obsolete("change requirement for turn on turn off, backlog: 13220")]
    private async Task<bool> IsActiveDevice(string? id)
    {
        if (!string.IsNullOrEmpty(id))
        {
            var activeDeviceId = await _deviceStatusService.GetActiveDevice(new List<string>() { id });
            if (activeDeviceId == null || activeDeviceId.Count < 1)
            {
                return false;
            }
        }
        return true;
    }


}

