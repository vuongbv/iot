using Common.Logging;
using Common.Mongodb.Repository;
using Common.MongodbMultitenant.Store;
using Cronos;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Newtonsoft.Json;
using SendGrid.Helpers.Mail;
using SendGrid;
using StackExchange.Redis;
using WE.IOT.Service.Application.Constants;
using WE.IOT.Service.Application.Models.TenantSetting;
using WE.IotService.Application.Models;
using WE.IOTService.Domain.Entities;
using WE.IOT.Service.SignalRHub;
using System.Collections.Concurrent;
using Common.SMS.Sender;
using Common.SMS;
using Common.Email;
using Common.MongoDb.Audits;
using MongoDB.Bson.Serialization.IdGenerators;
using WE.IOT.Service.Domain.Entities;
using WE.IOT.Service.Application.Models.Operation;
using System.Text.RegularExpressions;
using System;
using FluentValidation.Internal;

namespace WE.IOT.Service.Application.BackgroundJobs;

public class AlarmRuleBackgroundJob : IHostedService, IDisposable
{

    private System.Timers.Timer? _tenantTimer;
    private System.Timers.Timer? _lastDataTimer;
    private readonly CronExpression _tenantExpression;
    private readonly CronExpression _deviceStatusExpression;
    private ConcurrentBag<string> listTenant = new ConcurrentBag<string>();
    private ITenantStore _tenantRespository;
    private readonly IServiceScopeFactory _scopeFactor;
    private readonly ILogger _logger;
    private readonly IHubContext<IotHub> _iotHubContext;
    private readonly Common.SMS.Sender.ISmsSender _smsSender;
    private readonly SendGridOptions _options;
    private readonly IEmailSender _emailSender;

    public AlarmRuleBackgroundJob(
        ITenantStore tenantRespository,
        IServiceScopeFactory scopeFactor,
        ILogger logger,
        IHubContext<IotHub> iotHubContext,
        Common.SMS.Sender.ISmsSender smsSender,
        SendGridOptions options,
        IEmailSender emailSender
        )
    {
        _tenantExpression = CronExpression.Parse("0/10 * * * *");
        _deviceStatusExpression = CronExpression.Parse("0/1 * * * *");
        this._tenantRespository = tenantRespository ?? throw new ArgumentNullException(nameof(tenantRespository));
        this._scopeFactor = scopeFactor ?? throw new ArgumentNullException(nameof(scopeFactor));
        this._logger = logger;
        this._iotHubContext = iotHubContext ?? throw new ArgumentNullException(nameof(_iotHubContext));
        this._smsSender = smsSender ?? throw new ArgumentNullException(nameof(smsSender));
        this._options = options ?? throw new ArgumentNullException(nameof(options));
        this._emailSender = emailSender;
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

    private async Task HandleDeviceStatus(string database, IMongoClient iMongodbClient)
    {
        var tenantSettingRepository = new Repository<TenantSetting>(database, "TenantSetting", iMongodbClient);
        var tenantSettingRepo =       new Repository<BsonDocument>(database, "TenantSetting", iMongodbClient);

        var measurementRepository = new Repository<BsonDocument>(database, "Measurement", iMongodbClient);
        var deviceRepository = new Repository<Device>(database, "Device", iMongodbClient);

        var allUpdateDeviceAction = await tenantSettingRepository
            .FindAsyncWithoutDataAuthorize(x => x.Key == TenantSettingKeyConstants.Threshold);

        var allThresholdModel = allUpdateDeviceAction.ToList().Select( 
            x => BsonSerializer.Deserialize<ThresholdSettingModel>(x.Value.ToBsonDocument()))
            .ToList().Where(
            y => y.Actions.Any()
            && y.Actions.Where(y => y.Type == ActionType.UpdateDeviceStatus).Any()
            );

       
        #region allUpdateDeviceAction
        //
        foreach (var threshold in allThresholdModel)
        {
            var actions = threshold.Actions.Where(x => x.Type == ActionType.UpdateDeviceStatus);

            foreach (var action in actions)
            {
                var deviceId = threshold.Device;
                var amountTime = threshold?.ValueEqual != null ? int.Parse(threshold?.ValueEqual) : -1;
                var dataPath = threshold.Criteria;

                if (string.IsNullOrEmpty(dataPath) ||
                    amountTime == -1 ||
                    string.IsNullOrEmpty(dataPath)) continue;

                PipelineDefinition<BsonDocument, MeasurementData> pipeline = new BsonDocument[]
                    {
                                new BsonDocument("$match",
                                new BsonDocument("source.id", deviceId)),
                                new BsonDocument("$sort",
                                new BsonDocument("time", -1)),
                                new BsonDocument("$limit", 1),
                                new BsonDocument("$project",
                                new BsonDocument("time", "$time"))
                    };

                var currrentTime = DateTime.UtcNow;

                var measurementResult = (await measurementRepository.Collection.AggregateAsync(pipeline)).ToList();

                var lastRecord = measurementResult.Any() ? measurementResult.First() : null;

                if (lastRecord != null)
                {
                    var lastTimePlusAmount = lastRecord.Time.AddMinutes(amountTime);

                    if (lastTimePlusAmount > currrentTime)
                    {
                        // turn on device
                        var deviceEntity = (await deviceRepository.FindAsyncWithoutDataAuthorize(x => x.SourceId == deviceId && (x.Status == null || x.Status == DeviceConstants.ActiveDevice))).FirstOrDefault();

                        if (deviceEntity != null)
                        {
                            if (deviceEntity.OperationStatus != "On")
                            {
                                deviceEntity.UpdatedDate = DateTime.UtcNow;
                                deviceEntity.OperationStatus = "On";
                                await deviceRepository.UpdateAsync(deviceEntity, x => x.Id == deviceEntity.Id, default);
                            }
                        }
                        else
                        {
                            throw new Exception("Can't find device when update device status or maybe the device are inactive!");
                        }
                    }
                    else
                    {
                        // turn off device
                        var deviceEntity = (await deviceRepository.FindAsyncWithoutDataAuthorize(x => x.SourceId == deviceId && (x.Status == null || x.Status == DeviceConstants.ActiveDevice))).FirstOrDefault();

                        if (deviceEntity != null)
                        {
                            if (deviceEntity.OperationStatus != "Off")
                            {
                                deviceEntity.UpdatedDate = DateTime.UtcNow;
                                deviceEntity.OperationStatus = "Off";
                                await deviceRepository.UpdateAsync(deviceEntity, x => x.Id == deviceEntity.Id, default);
                            }
                        }
                        else
                        {
                            throw new Exception("Can't find device when update device status or maybe the device are inactive!");
                        }
                    }
                }
            }
        }
        #endregion

        #region No_data

        // Fixed limit operator alarm
     
        var allThresholdLimitModel = allUpdateDeviceAction.ToList().Select(
           x => BsonSerializer.Deserialize<ThresholdSettingModel>(x.Value.ToBsonDocument()))
           .ToList().Where(x => x.Operator == Operator.no_data);



        foreach (var limitOp in allThresholdLimitModel)
        {
            var deviceId = limitOp.Device;
            var deviceEntity = (await deviceRepository.FindAsyncWithoutDataAuthorize(x => x.SourceId == deviceId)).FirstOrDefault();

            var lstRuls = tenantSettingRepo.Collection.AsQueryable().ToList().Where(i => i.Elements.Any(x => x.Value == "Threshold"));

            var matchingRule = lstRuls.Where(x => x["Value"]["Device"].ToString().ToLower().Equals(deviceEntity.SourceId.ToString().ToLower()));

           

            // Get last record
            PipelineDefinition<BsonDocument, MeasurementData> pipeline = new BsonDocument[]
                  {
                                new BsonDocument("$match",
                                new BsonDocument("source.id", deviceId)),
                                new BsonDocument("$sort",
                                new BsonDocument("time", -1)),
                                new BsonDocument("$limit", 1),
                                new BsonDocument("$project",
                                new BsonDocument("time", "$time"))
                  };

            var currrentTime = DateTime.UtcNow;
            var measurementResult = (await measurementRepository.Collection.AggregateAsync(pipeline)).ToList();
            var lastRecord = measurementResult.Any() ? measurementResult.First() : null;
            TimeSpan ts = DateTime.Now - lastRecord.Time;
            if (ts.TotalMinutes > Convert.ToDouble(limitOp.ValueEqual))
            {
                int index = 0;
                foreach (var item in limitOp.Actions)
                {
                    index += 1;
                    var lastTime = item.ToBsonDocument().GetValue("LastTrigger", null);
                    var frequency = item.ToBsonDocument().GetValue("Frequency", null);
                    var lstActionsUpdate = new BsonArray();
                    if (frequency.RawValue == null || (frequency.RawValue != null && (lastTime.RawValue == null || Convert.ToDateTime(lastTime).AddMinutes(frequency.ToDouble()) < DateTime.UtcNow)))
                    {
                        var siTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
                        DateTime siTime = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.Local, siTimeZone);

                        #region SMS
                        if (item.Type == ActionType.SMS)
                        {

                           


                            var lstRecipientSetting = tenantSettingRepo.Collection.AsQueryable().ToList().Where(i => i.Elements.Any(x => x.Value == "AlarmRecepient"));

                            var recipientSetting = lstRecipientSetting.FirstOrDefault(x => x["Value"]["ListName"].ToString().ToLower().Equals(item.Recipient != null ? item.Recipient.ToString().ToLower() : ""));

                            if (recipientSetting != null)
                            {
                                var lstRecipient = new List<string>();
                                lstRecipient = recipientSetting["Value"]["Recepient"].AsBsonArray.Select(x => x.ToString()).ToList();


                                await _smsSender.SendSmsAsync(lstRecipient, "Device " + limitOp.Device.ToString() + " " + item.Description + " at: " + siTime.ToString());
                            }
                            else
                            {
                                var contacts = item.Recipient.ToString().Split(",").ToList().Where(x => VerifyPhone(x)).ToList();
                                if (contacts.Any())
                                {
                                    await _smsSender.SendSmsAsync(contacts, "Device " + limitOp.Device.ToString() + " " + item.Description + " at: " + siTime.ToString());
                                }

                            }


                            //}

                        }
                        #endregion
                        #region email
                        else if (item.Type == ActionType.Email)
                        {
                            // var lastTime = item.ToBsonDocument().GetValue("LastTrigger", null);
                            // var frequency = item.ToBsonDocument().GetValue("Frequency", null);
                            //   if (frequency.RawValue == null || (frequency.RawValue != null && (lastTime.RawValue == null || ((DateTime)lastTime).AddMinutes(frequency.ToDouble()) < DateTime.UtcNow)))
                            //{
                            var lstRecipientSetting = tenantSettingRepo.Collection.AsQueryable().ToList().Where(i => i.Elements.Any(x => x.Value == "AlarmRecepient"));

                            var recipientSetting = lstRecipientSetting.FirstOrDefault(x => x["Value"]["ListName"].ToString().ToLower().Equals(item.Recipient.ToString().ToLower()));
                            if (recipientSetting != null)
                            {
                                var lstRecipient = new List<string>();
                                lstRecipient = recipientSetting["Value"]["Recepient"].AsBsonArray.Select(x => x.ToString()).ToList();
                                var client = new SendGridClient(_options.SendGridAPIKey);
                                var msg = new SendGridMessage()
                                {
                                    From = new EmailAddress(_options.Sender),
                                    Subject = "Alarm Notification for " + deviceEntity.SourceId.ToString(),
                                    // HtmlContent = "Device " + entity["source"]["id"].ToString() + " " + item["Description"].ToString() + " at: " + DateTime.UtcNow + " UTC"
                                    // 16739 he datetime in the content of email notifications are in UtC. It should be translated to local time to avoid confusion.   
                                    HtmlContent = "Device " + limitOp.Device.ToString() + " " + item.Description + " at: " + siTime.ToString()
                                };
                                foreach (var email in lstRecipient)
                                {
                                    msg.AddTo(new EmailAddress(email));
                                }
                                // var response = await client.SendEmailAsync(msg);
                                await _emailSender.SendAsync(lstRecipient, msg.Subject, msg.HtmlContent);

                            }
                            else
                            {
                                var contact = item.Recipient.ToString().Split(",");
                                var client = new SendGridClient(_options.SendGridAPIKey);
                                var msg = new SendGridMessage()
                                {
                                    From = new EmailAddress(_options.Sender),
                                    Subject = "Alarm Notification for " + deviceEntity.SourceId.ToString().ToString(),
                                    //HtmlContent = "Device " + entity["source"]["id"].ToString() + " " + item["Description"].ToString() + " at: " + DateTime.UtcNow + " UTC"
                                    // 16739 he datetime in the content of email notifications are in UtC. It should be translated to local time to avoid confusion.   
                                    HtmlContent = "Device " + limitOp.Device.ToString() + " " + item.Description + " at: " + siTime.ToString()
                                };
                                var valid = false;
                                foreach (var email in contact)
                                {
                                    if (VerifyEmail(email))
                                    {
                                        valid = true;
                                        msg.AddTo(new EmailAddress(email));
                                    }
                                }
                                if (valid)
                                {
                                    // var response = await client.SendEmailAsync(msg);
                                    await _emailSender.SendAsync(contact.ToList(), msg.Subject, msg.HtmlContent);
                                }
                            }

                        }
                        #endregion
                        #region alarm
                        else if (item.Type == ActionType.Alarm)
                        {

                            var alarm = new Alarm();
                            alarm.CreatedDate = DateTime.UtcNow;
                            var deviceName = (await deviceRepository.FindAsync(x => x.SourceId == deviceId)).ToList().FirstOrDefault().Name;

                            alarm.Source = new DeviceAlarm()
                            {
                                Name = deviceName != null ? deviceName : String.Empty,
                                Id = deviceEntity.SourceId.ToString()
                            };
                            alarm.Type = item.AlarmType.ToString();
                            alarm.Text = item.Description != null ? item.Description.ToString() : "";
                            alarm.Severity = item.SeverityType.ToString();
                            alarm.Status = AlarmConstants.Active;
                            alarm.Time = DateTime.UtcNow;
                            var alarmRepo = new Repository<Alarm>(database, "Alarm", iMongodbClient);

                            await alarmRepo.InsertAsync(alarm);

                            await SendMeasurementSignalChange(database, string.Empty, deviceEntity.SourceId.ToString());

                        }


                        if (frequency.RawValue == null || (frequency.RawValue != null && (lastTime.RawValue == null || Convert.ToDateTime(lastTime).AddMinutes(frequency.ToDouble()) < DateTime.UtcNow)))
                        {
                            foreach (var rule in matchingRule)
                            {
                                if (rule["Value"]["Operator"].ToInt32() == (int)Operator.no_data)
                                {
                                    try
                                    {
                                        rule["Value"]["Actions"][index-1]["LastTrigger"] = DateTime.UtcNow;
                                        await tenantSettingRepo.UpdateAsync(rule, x => x["_id"] == rule["_id"], default);
                                    }

                                    catch (Exception ex)
                                    {
                                        throw ex;
                                    }


                                }
                            }
                        }

                        #endregion
                    }

                }
            }

        }
            #endregion
    }

    private async Task HandleGeoFancing(string database, IMongoClient iMongodbClient)
    {
        var tenantSettingRepository = new Repository<TenantSetting>(database, "TenantSetting", iMongodbClient);
        var measurementRepository = new Repository<BsonDocument>(database, "Measurement", iMongodbClient);
        var deviceRepository = new Repository<Device>(database, "Device", iMongodbClient);
        var auditRepository = new Repository<Audit>(database, "Audit", iMongodbClient);
        var operationRepository = new Repository<Operation>(database, "Operation", iMongodbClient);

        var allUpdateDeviceAction = await tenantSettingRepository
            .FindAsyncWithoutDataAuthorize(x => x.Key == TenantSettingKeyConstants.Threshold);

        var allThresholdModel = allUpdateDeviceAction.ToList().Select(
            x => BsonSerializer.Deserialize<ThresholdSettingModel>(x.Value.ToBsonDocument()))
            .ToList().Where(x => x.Operator == Operator.moving_in ||
                                    x.Operator == Operator.moving_out ||
                                    x.Operator == Operator.moving_in_out);

        foreach (var threshold in allThresholdModel)
        {
            if (!string.IsNullOrEmpty(threshold.Geometry))
            {
                var geoJson = threshold.Geometry;

                Geometry geometry;

                var serializer = GeoJsonSerializer.Create();

                using (var stringReader = new StringReader(geoJson))
                using (var jsonReader = new JsonTextReader(stringReader))
                {
                    geometry = serializer.Deserialize<Geometry>(jsonReader);
                }

                PipelineDefinition<Audit, AuditGps> pipeline = new BsonDocument[]
                {
                    new BsonDocument("$match",
                    new BsonDocument("Type", "Tracking_GPS")),
                    new BsonDocument("$addFields",
                    new BsonDocument("DateTime",
                    new BsonDocument("$toDate", "$CreatedDate"))),
                    new BsonDocument("$sort",
                    new BsonDocument("DateTime", -1)),
                    new BsonDocument("$group",
                    new BsonDocument
                        {
                            { "_id", "$AfterValue.source_id" },
                            { "Items",
                    new BsonDocument("$push",
                    new BsonDocument
                                {
                                    { "SourceId", "$AfterValue.source_id" },
                                    { "Lat", "$AfterValue.latitude" },
                                    { "Lng", "$AfterValue.longitude" }
                                }) }
                        }),
                    new BsonDocument("$sort",
                    new BsonDocument("DateTime", -1)),
                    new BsonDocument("$project",
                    new BsonDocument
                        {
                            { "SourceId", "$_id" },
                            { "First",
                    new BsonDocument("$first", "$Items") }
                        }),
                    new BsonDocument("$project",
                    new BsonDocument
                        {
                            { "SourceId", "$SourceId" },
                            { "Lat", "$First.Lat" },
                            { "Lng", "$First.Lng" }
                        })
                };

                PipelineDefinition<Operation, GeofencingOperation> pipelineGeofencingOperation = new BsonDocument[]
                {
                     new BsonDocument("$match",
                        new BsonDocument
                            {
                                { "Type", OperationConstants.GeoFencingOprationType },
                                { "Source", threshold.Device }
                            }),
                        new BsonDocument("$project",
                        new BsonDocument
                            {
                                { "SourceId", "$Source" },
                                { "Lat", "$OperationData.Lat" },
                                { "Lng", "$OperationData.Lng" },
                                { "Geometry", "$OperationData.Geometry" },
                                { "Type", "$OperationData.Type" },
                                { "CreatedDate", "$CreatedDate" }
                            }),
                        new BsonDocument("$sort",
                        new BsonDocument("CreatedDate", -1)),
                        new BsonDocument("$project",
                        new BsonDocument
                            {
                                { "SourceId", "$SourceId" },
                                { "Lat", "$Lat" },
                                { "Lng", "$Lng" },
                                { "Geometry", "$Geometry" },
                                { "Type", "$Type" }
                            })
                };


                var auditGps = auditRepository.Collection.Aggregate<AuditGps>(pipeline).ToList().Where( x=> x.SourceId != null && x.SourceId == threshold.Device).ToList();

                var deviceWithLocation = auditGps.Select(x =>
                {
                    return new DeviceWithLocationGeometry()
                    {
                        SourceId = x.SourceId,
                        Point = new Point(new Coordinate(
                                       Double.Parse(x.Lng),
                                        Double.Parse(x.Lat)
                                   ))
                    };
                }).First();

                var geoFencingOperations = operationRepository.Collection.Aggregate<GeofencingOperation>(pipelineGeofencingOperation).ToList();

                var currentPositionAndGeometry = auditGps.Select(x =>
                {
                    return new CurrrentDeviceGeofencing()
                    {
                        SourceId = x.SourceId,
                        Lat = x.Lat,
                        Lng = x.Lng,
                        Geometry = threshold.Geometry
                    };
                }).First();

                switch (threshold.Operator)
                {
                    case Operator.moving_in:
                        {

                            if (geometry.Contains(deviceWithLocation.Point))
                            {
                                await HandleAction(database, iMongodbClient, threshold.Actions, currentPositionAndGeometry, geoFencingOperations);
                            }

                            break;
                        }
                    case Operator.moving_out:
                        {

                            if (!geometry.Contains(deviceWithLocation.Point))
                            {
                                await HandleAction(database, iMongodbClient, threshold.Actions, currentPositionAndGeometry, geoFencingOperations);
                            }

                            break;
                        }
                    case Operator.moving_in_out:
                        {
                            if (geometry.Contains(deviceWithLocation.Point) || !geometry.Contains(deviceWithLocation.Point))
                            {
                                await HandleAction(database, iMongodbClient, threshold.Actions, currentPositionAndGeometry, geoFencingOperations);
                            }

                            break;
                        }
                }
            }
        }
    }

    private async Task HandleAction(
        string database,
        IMongoClient iMongodbClient,
        List<WE.IOT.Service.Application.Models.TenantSetting.Action> actions,
        CurrrentDeviceGeofencing devices,
        List<GeofencingOperation> geofencingOperations
        )
    {
        //process Action
        var tenantSettingRepo = new Repository<BsonDocument>(database, "TenantSetting", iMongodbClient);
        var operationRepository = new Repository<Operation>(database, "Operation", iMongodbClient);
        var deviceRepository = new Repository<Device>(database, "Device", iMongodbClient);

        foreach (var item in actions)
        {
            if (item.Type == ActionType.SMS)
            {
                var previousOperation = geofencingOperations
                    .Where(x => x.SourceId == devices.SourceId &&
                    x.Lat == devices.Lat &&
                    x.Lng == devices.Lng &&
                    x.Type == OperationConstants.GeoFencingSmsOprationAction).FirstOrDefault();

                if (previousOperation == null || (previousOperation.Lat != devices.Lat || previousOperation.Lng != devices.Lng || previousOperation.Geometry != devices.Geometry))
                {
                    var lstRecipientSetting = tenantSettingRepo.Collection.AsQueryable().ToList().Where(i => i.Elements.Any(x => x.Value == "AlarmRecepient"));

                    var recipientSetting = lstRecipientSetting.FirstOrDefault(x => x["Value"]["ListName"].ToString().ToLower().Equals(item.Recipient.ToString().ToLower()));
                    var siTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
                    DateTime siTime = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.Local, siTimeZone);

                    if (recipientSetting != null)
                    {
                        var lstRecipient = new List<string>();
                        lstRecipient = recipientSetting["Value"]["Recepient"].AsBsonArray.Select(x => x.ToString()).ToList();
                        //await _smsSender.SendSmsAsync(lstRecipient, "Device " + devices.SourceId.ToString() + " " + item.Description != null ? item.Description.ToString() : "" + " at: " + DateTime.UtcNow + " UTC");
                        await _smsSender.SendSmsAsync(lstRecipient, "Device " + devices.SourceId.ToString() + " " + item.Description != null ? item.Description.ToString() : "" + " at: " + siTime.ToString());

                    }
                    else
                    {
                        var contacts = item.Recipient.ToString().Split(",").ToList().Where(x => VerifyPhone(x)).ToList();
                        if (contacts.Any())
                        {
                            //await _smsSender.SendSmsAsync(contacts, "Device " + devices.SourceId.ToString() + " " + item.Description != null ? item.Description.ToString() : "" + " at: " + DateTime.UtcNow + " UTC");
                            await _smsSender.SendSmsAsync(contacts, "Device " + devices.SourceId.ToString() + " " + item.Description != null ? item.Description.ToString() : "" + " at: " + siTime.ToString());
                        }

                    }

                    var operation = new WE.IOT.Service.Domain.Entities.Operation()
                    {
                        CNO = Guid.NewGuid().ToString(),
                        CreatedDate = DateTime.UtcNow,
                        Source = devices.SourceId,
                        Type = OperationConstants.GeoFencingOprationType,
                        OperationData = new
                        {
                            SourceId = devices.SourceId,
                            Lat = devices.Lat,
                            Lng = devices.Lng,
                            Geometry = devices.Geometry,
                            Type = OperationConstants.GeoFencingSmsOprationAction
                        }.ToBsonDocument()
                    };

                    await operationRepository.InsertAsync(operation);
                }

            }
            if (item.Type == ActionType.Email)
            {
                var previousOperation = geofencingOperations
                    .Where(x => x.SourceId == devices.SourceId &&
                    x.Lat == devices.Lat &&
                    x.Lng == devices.Lng &&
                    x.Type == OperationConstants.GeoFencingEmailOprationAction).FirstOrDefault();

                if (previousOperation == null || (previousOperation.Lat != devices.Lat || previousOperation.Lng != devices.Lng || previousOperation.Geometry != devices.Geometry))
                {
                    var lstRecipientSetting = tenantSettingRepo.Collection.AsQueryable().ToList().Where(i => i.Elements.Any(x => x.Value == "AlarmRecepient"));

                    var recipientSetting = lstRecipientSetting.FirstOrDefault(x => x["Value"]["ListName"].ToString().ToLower().Equals(item.Recipient.ToString().ToLower()));
                    if (recipientSetting != null)
                    {
                        var lstRecipient = new List<string>();
                        lstRecipient = recipientSetting["Value"]["Recepient"].AsBsonArray.Select(x => x.ToString()).ToList();
                        var client = new SendGridClient(_options.SendGridAPIKey);
                        var msg = new SendGridMessage()
                        {
                            From = new EmailAddress(_options.Sender),
                            Subject = "Alarm Notification for " + devices.SourceId.ToString(),
                            //HtmlContent = "Device " + devices.SourceId.ToString() + " " + item.Description != null ? item.Description.ToString() : "" + " at: " + DateTime.UtcNow + " UTC"
                            // 16739 he datetime in the content of email notifications are in UtC. It should be translated to local time to avoid confusion.   
                            HtmlContent = "Device " + devices.SourceId.ToString() + " " + item.Description != null ? item.Description.ToString() : "" + " at: " + DateTime.Now
                        };
                        foreach (var email in lstRecipient)
                        {
                            msg.AddTo(new EmailAddress(email));
                        }
                        //var response = await client.SendEmailAsync(msg);
                        await _emailSender.SendAsync(lstRecipient, msg.Subject, msg.HtmlContent);
                    }
                    else
                    {
                        var contact = item.Recipient.ToString().Split(",");
                        var client = new SendGridClient(_options.SendGridAPIKey);
                        var msg = new SendGridMessage()
                        {
                            From = new EmailAddress(_options.Sender),
                            Subject = "Alarm Notification for " + devices.SourceId.ToString(),
                            //HtmlContent = "Device " + devices.SourceId.ToString() + " " + item.Description != null ? item.Description.ToString() : "" + " at: " + DateTime.UtcNow + " UTC"
                            // 16739 he datetime in the content of email notifications are in UtC. It should be translated to local time to avoid confusion.   
                            HtmlContent = "Device " + devices.SourceId.ToString() + " " + item.Description != null ? item.Description.ToString() : "" + " at: " + DateTime.Now

                        };
                        var valid = false;
                        foreach (var email in contact)
                        {
                            if (VerifyEmail(email))
                            {
                                valid = true;
                                msg.AddTo(new EmailAddress(email));
                            }
                        }
                        if (valid)
                        {
                            // var response = await client.SendEmailAsync(msg);
                            await _emailSender.SendAsync(contact.ToList(), msg.Subject, msg.HtmlContent);
                        }

                    }

                    var operation = new WE.IOT.Service.Domain.Entities.Operation()
                    {
                        CNO = Guid.NewGuid().ToString(),
                        CreatedDate = DateTime.UtcNow,
                        Source = devices.SourceId,
                        Type = OperationConstants.GeoFencingOprationType,
                        OperationData = new
                        {
                            SourceId = devices.SourceId,
                            Lat = devices.Lat,
                            Lng = devices.Lng,
                            Geometry = devices.Geometry,
                            Type = OperationConstants.GeoFencingEmailOprationAction
                        }.ToBsonDocument()
                    };

                    await operationRepository.InsertAsync(operation);
                }
            }
            if (item.Type == ActionType.Alarm)
            {

                var previousOperation = geofencingOperations
                    .Where(x => x.SourceId == devices.SourceId &&
                    x.Lat == devices.Lat &&
                    x.Lng == devices.Lng &&
                    x.Type == OperationConstants.GeoFencingAlarmOprationAction).FirstOrDefault();

                if (previousOperation == null || (previousOperation.Lat != devices.Lat || previousOperation.Lng != devices.Lng || previousOperation.Geometry != devices.Geometry))
                {
                    var alarm = new Alarm();
                    alarm.CreatedDate = DateTime.UtcNow;
                    var deviceName = (await deviceRepository.FindAsync(x => x.SourceId == devices.SourceId)).ToList().FirstOrDefault().Name;

                    alarm.Source = new DeviceAlarm()
                    {
                        Name = deviceName !=null? deviceName: String.Empty,
                        Id = devices.SourceId.ToString()
                    };
                    alarm.Type = item.AlarmType.ToString();
                    alarm.Text = item.Description != null ? String.Empty : item.Description.ToString();
                    alarm.Severity = item.SeverityType.ToString();
                    alarm.Status = AlarmConstants.Active;
                    alarm.Time = DateTime.UtcNow;

                    var alarmRepo = new Repository<Alarm>(database, "Alarm", iMongodbClient);

                    await alarmRepo.InsertAsync(alarm);

                    await SendMeasurementSignalChange(database, string.Empty, devices.SourceId.ToString());


                    var operation = new WE.IOT.Service.Domain.Entities.Operation()
                    {
                        CNO = Guid.NewGuid().ToString(),
                        CreatedDate = DateTime.UtcNow,
                        Source = devices.SourceId,
                        Type = OperationConstants.GeoFencingOprationType,
                        OperationData = new
                        {
                            SourceId = devices.SourceId,
                            Lat = devices.Lat,
                            Lng = devices.Lng,
                            Geometry = devices.Geometry,
                            Type = OperationConstants.GeoFencingAlarmOprationAction
                        }.ToBsonDocument()
                    };

                    await operationRepository.InsertAsync(operation);
                }
            }

        }
    }
    private static bool VerifyEmail(string input)
    {
        // Define the regex patterns for email and phone number
        //string emailPattern = @"^[^@\s] +@[^@\s\.]+\.[^@\.\s]+$";
        string emailPattern = @"^[a-zA-Z0-9.!#$%&'*+-/=?^_`{|}~]+@[a-zA-Z0-9-]+(?:\.[a-zA-Z0-9-]+)*$";
        // Create Regex objects for email and phone number
        Regex emailRegex = new Regex(emailPattern);

        // Test the input string against the regexes
        bool isEmail = emailRegex.IsMatch(input);

        // Return true if the input is either a valid email or a valid phone number
        return isEmail;
    }
    private static bool VerifyPhone(string input)
    {
        // Define the regex patterns for email and phone number
        string phonePattern = @"^\+? (\d.*){ 3,}$";

        // Create Regex objects for email and phone number
        Regex phoneRegex = new Regex(phonePattern);

        // Test the input string against the regexes
        bool isPhone = phoneRegex.IsMatch(input);

        // Return true if the input is either a valid email or a valid phone number
        return isPhone;
    }
    private async Task SendMeasurementSignalChange(string tenantName, string panelId, string deviceId)
    {
        if (panelId != null && deviceId != null)
        {
            await _iotHubContext.Clients
               .Group(IotHub.IotHubGroup + tenantName)
               .SendAsync(IotHub.MeasurementChange,
                           new MeasurementSignalrModel
                           {
                               DeviceId = deviceId,
                               PanelId = panelId
                           }
               );
        }
    }


    public async Task DoLastDataWork(CancellationToken cancellationToken)
    {
        try
        {
            // get thres hold 
            using (var scope = _scopeFactor.CreateScope())
            {
                var iMongodbClient = scope.ServiceProvider.GetRequiredService<IMongoClient>();
                var listDb = await iMongodbClient.ListDatabaseNamesAsync(cancellationToken);

                var existedDb = listDb.ToList();

                foreach (var database in listTenant)
                {
                    if (existedDb.Contains(database))
                    {
                        await HandleDeviceStatus(database, iMongodbClient);
                        await HandleGeoFancing(database, iMongodbClient);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex.Message);
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

public class MeasurementData
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
    [BsonElement("time")]
    public DateTime Time { get; set; }
}


public class DeviceWithLocationGeometry
{
    public string SourceId { get; set; }
    public NetTopologySuite.Geometries.Point Point { get; set; }
}

public class GeofencingOperation
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
    public string SourceId { get; set; } = default!;
    public string Lat { get; set; } = default!;
    public string Lng { get; set; } = default!;
    public string Geometry { get; set; } = default!;
    public string Type { get; set; } = default!;
}

public class CurrrentDeviceGeofencing
{

    public string SourceId { get; set; } = default!;
    public string Lat { get; set; } = default!;
    public string Lng { get; set; } = default!;
    public string Geometry { get; set; } = default!;
}

public class AuditGps
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
    public string SourceId { get; set; }
    public string Lng { get; set; }
    public string Lat { get; set; }
}

public static class AlarmRuleBackgroundJobExtension
{
    public static void AddAlarmRuleBackgroupdJob(this IServiceCollection services)
    {
        services.AddHostedService<AlarmRuleBackgroundJob>();
    }
}
