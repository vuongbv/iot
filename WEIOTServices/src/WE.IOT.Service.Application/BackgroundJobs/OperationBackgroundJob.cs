
using Common.Logging;
using Common.Mongodb.Repository;
using Common.MongodbMultitenant.Store;
using Common.RestClientHelpers;
using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using WE.IOT.Service.Application.Constants;
using WE.IOT.Service.Application.Models.Operation;
using WE.IOT.Service.Application.Models.TenantSetting;
using WE.IOTService.Domain.Entities;
using WE.IOT.Service.Domain.Entities;
using System.Collections.Concurrent;

namespace WE.IOT.Service.Application.BackgroundJobs;

public class OperationBackgroundJob : IHostedService, IDisposable
{
    private System.Timers.Timer? _tenantTimer;
    private System.Timers.Timer? _lastDataTimer;
    private readonly CronExpression _tenantExpression;
    private readonly CronExpression _deviceStatusExpression;
    private ConcurrentBag<string> listTenant = new ConcurrentBag<string>();
    private ITenantStore _tenantRespository;
    private readonly IServiceScopeFactory _scopeFactor;
    private readonly TurnOnOffDeviceClient _turnOnOffDeviceClient;
    private readonly ILogger _logger;

    public OperationBackgroundJob(
        ITenantStore tenantRespository,
        IServiceScopeFactory scopeFactor,
        TurnOnOffDeviceClient turnOnOffDeviceClient,
        ILogger logger
        )
    {
        _tenantExpression = CronExpression.Parse("0/10 * * * *");
        _deviceStatusExpression = CronExpression.Parse("0/1 * * * *");
        this._tenantRespository = tenantRespository ?? throw new ArgumentNullException(nameof(tenantRespository));
        this._scopeFactor = scopeFactor ?? throw new ArgumentNullException(nameof(scopeFactor));
        this._turnOnOffDeviceClient = turnOnOffDeviceClient ?? throw new ArgumentNullException(nameof(turnOnOffDeviceClient));
        this._logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await DoTenantWork(cancellationToken);

        await ScheduleTenantJob(cancellationToken);
        await ScheduleLastDataJob(cancellationToken);
    }


    protected virtual async Task ScheduleTenantJob(CancellationToken cancellationToken)
    {
        var next = _tenantExpression.GetNextOccurrence(DateTimeOffset.Now, TimeZoneInfo.Utc);

        if (next.HasValue)
        {
            var delay = next.Value - DateTimeOffset.Now;

            if (delay.TotalMilliseconds <= 0)
            {
                await ScheduleTenantJob(cancellationToken);
            }
            _tenantTimer = new System.Timers.Timer(delay.TotalMilliseconds);

            _tenantTimer.Elapsed += async (_, _) =>
            {
                _tenantTimer.Dispose();
                _tenantTimer = null;

                if (!cancellationToken.IsCancellationRequested)
                {
                    await DoTenantWork(cancellationToken);
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    await ScheduleTenantJob(cancellationToken);
                }
            };

            _tenantTimer.Start();
        }
        await Task.CompletedTask;
    }

    public async Task DoTenantWork(CancellationToken cancellationToken)
    {
        var tenants = this._tenantRespository.GetAll().Where(x =>
                                                                x.Database != null &&
                                                                !string.IsNullOrEmpty(x.Database)
                                                        ).Select(x => x.Database);

        this.listTenant = new ConcurrentBag<string>(tenants.ToList());

    }

    protected async Task ScheduleLastDataJob(CancellationToken cancellationToken)
    {
        var next = _deviceStatusExpression.GetNextOccurrence(DateTimeOffset.Now, TimeZoneInfo.Utc);

        if (next.HasValue)
        {
            var delay = next.Value - DateTimeOffset.Now;

            if (delay.TotalMilliseconds <= 0)
            {
                await ScheduleLastDataJob(cancellationToken);
            }
            _lastDataTimer = new System.Timers.Timer(delay.TotalMilliseconds);
            _lastDataTimer.Elapsed += async (_, _) =>
            {
                _lastDataTimer.Dispose();
                _lastDataTimer = null;

                if (!cancellationToken.IsCancellationRequested)
                {
                    await DoLastDataWork(cancellationToken);
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    await ScheduleLastDataJob(cancellationToken);
                }
            };

            _lastDataTimer.Start();
        }
        await Task.CompletedTask;
    }

    public async Task DoLastDataWork(CancellationToken cancellationToken)
    {
        try
        {
            using (var scope = _scopeFactor.CreateScope())
            {
                var iMongodbClient = scope.ServiceProvider.GetRequiredService<IMongoClient>();
                var listDb = await iMongodbClient.ListDatabaseNamesAsync(cancellationToken);

                var existedDb = listDb.ToList();

                foreach (var database in listTenant)
                {
                    if (existedDb.Contains(database))
                    {
                        var operationRepository = new Repository<Operation>(database, "Operation", iMongodbClient);
                        var deviceRepository = new Repository<Device>(database, "Device", iMongodbClient);

                        var listOperation = (await operationRepository.FindAsyncWithoutDataAuthorize(x => x.CommandStatus == OperationConstants.OperationProcessing || x.CommandStatus == OperationConstants.OperationFailed)).ToList();

                        var currentTime = DateTimeOffset.UtcNow;

                        foreach (var operation in listOperation)
                        {
                            if (operation.ExecutionTime <= currentTime)
                            {
                                var currentDevice = deviceRepository.GetFirstOrDefault(x => x.SourceId == operation.Source && x.Status == "Active");
                                var pinState = operation.Command == OperationConstants.TurnOn ? DevicePowerStatus.On : DevicePowerStatus.Off;

                                try
                                {
                                    var result = await _turnOnOffDeviceClient.CreatePowerCommand(operation.Source, operation.PinNumber, (int)pinState);
                                    if (result.success)
                                    {
                                        operation.Point = currentDevice.OperationStatus;
                                        operation.UpdatedDate = DateTime.UtcNow;
                                        operation.CommandStatus = OperationConstants.OperationProcessed;
                                        operation.PointStatus = operation.Command == OperationConstants.TurnOn ? OperationConstants.StatusOn : OperationConstants.StatusOff;
                                        operation.CommandExecutionMesssage = "Execute successful!";

                                        currentDevice.OperationStatus = operation.Command == OperationConstants.TurnOn ? OperationConstants.StatusOn : OperationConstants.StatusOff;
                                        currentDevice.UpdatedDate = DateTime.UtcNow;

                                        await operationRepository.UpdateAsync(operation, x => x.Id == operation.Id, default);
                                        await deviceRepository.UpdateAsync(currentDevice, x => x.Id == currentDevice.Id, default);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    operation.CommandExecutionMesssage = ex.Message;
                                    operation.CommandStatus = OperationConstants.OperationFailed;
                                    await operationRepository.UpdateAsync(operation, x => x.Id == operation.Id, default);

                                    _logger.Error(ex.Message);
                                }
                            }

                        }
                    }
                }
            }

        }
        catch (Exception e)
        {
            _logger.Error(e.Message);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _tenantTimer?.Stop();
        _lastDataTimer?.Stop();
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        _tenantTimer?.Dispose();
        _lastDataTimer?.Dispose();
        GC.SuppressFinalize(this);
    }
}


public static class OperationBackgroundJobExtension
{
    public static void AddOperationBackgroupdJob(this IServiceCollection services)
    {
        services.AddHostedService<OperationBackgroundJob>();
    }
}
