using AutoMapper;
using Common.Email;
using Common.Exceptions;
using Common.Extensions;
using Common.Logging;
using Common.MongoDb;
using Common.MongoDb.Repository;
using Common.MongoDbMultitenant;
using Common.SMS;
using Common.SMS.Sender;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Newtonsoft.Json;
using SendGrid;
using SendGrid.Helpers.Mail;
using WE.IOT.Service.Application.Constants;
using WE.IOT.Service.Application.Dtos.Measurement;
using WE.IOT.Service.Application.Models.Measurement;
using WE.IOT.Service.Application.Models.TenantSetting;
using WE.IotService.Application.Models;
using WE.IOTService.Domain.Entities;
using WE.IOTService.Domain.Store;
using WE.IOT.Service.Domain.Repository;
using WE.IOT.Service.SignalRHub;
using System.Dynamic;
using Tenant = Common.Mongodb.MultiTenant.Tenant;
using static WE.IOT.Service.Application.Constants.DataConstants;
using System.Linq.Expressions;
using MongoDB.Bson.Serialization;
using MongoDB.Driver.Linq;
using System.Collections.Generic;
using System.Globalization;
using Common.RestSharpClient;
using RestSharp;
using Common.Mongodb.MultiTenant;
using Microsoft.AspNetCore.Http;
using WE.IOT.Service.Application.Dto.Measurement;
using static WE.IOTService.Domain.Entities.RouterStatus;
using NLog.Config;
using static WE.IOTService.Domain.Entities.RouterSignal;
using Microsoft.Graph;
using static WE.IOTService.Domain.Entities.Router5GLatency;
using static WE.IOTService.Domain.Entities.Router5GDownload;
using static WE.IOTService.Domain.Entities.Router5GUpload;
using Calendar = System.Globalization.Calendar;
using DayOfWeek = System.DayOfWeek;
using EmailAddress = SendGrid.Helpers.Mail.EmailAddress;
using System.Drawing;
using Common.SMS.Exceptions;
using System.Net;
using System.Runtime;
using MongoDB.Driver.Core.WireProtocol.Messages;
using System.Text;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Net.Http;
using WE.IOT.Service.Domain.Entities;
using System.Data;
using System.Text.RegularExpressions;
using Common.MongoDb.Entities;
using MongoDB.Queries;
using Microsoft.Extensions.Hosting;
using Microsoft.Identity.Client;
using Common.Mongodb;
using Microsoft.Graph.ExternalConnectors;

namespace WE.IOT.Service.Application.Services
{
    public interface IMeasurementServices
    {
        public Task<dynamic> Create(CreateMeasurementModel model);
        public Task<List<dynamic>> GetAll();
        public Task<List<dynamic>> SearchForWatsons(FilterMeasurementModel model);
        public Task<List<dynamic>> SearchGeneric(FilterMeasurementModel model);
        public Task<bool> Update(string id, UpdateMeasurementModel moidel);
        public Task<bool> Delete(string id);
        public Task<TotalPowerDto> GetPowerForWatsons(string? id, IEnumerable<string> channelNames, DataConstants.Periodicity? periodicity);
        public Task<TotalPowerDto> GetGenericPower(string? id, string? subId, string? tenant, DataConstants.Periodicity? periodicity);
        public Task<DeviceBatteryDto> GetGenericBatteryIndicator(string id);
        public Task<TemperatureDto> GetTemperatureIndicator(string id);
        public Task<FiveGDto> GetFiveGIndicator(string id);
        public Task<PowerSunlightGeneratedHourlyDto> GetPowerGeneratedHourly(string id, string? irrId);
        public Task<List<SingleChartQueryResult>> GetFiveGSignalHourly(string id);
        public Task<List<string>> ExportGeneric(FilterMeasurementModel model);
        public Task<SingleValueDto> GetGenericSingleValue(GenericParamModel model);
        public Task<List<SingleChartDto>> GetGenericSingleChartValue(GenericParamModel model);
        public Task<bool> GetDataRouterStatus(string devices);
        public Task<bool> GetDataRouterSignal(string devices);
        public Task<bool> GetDataRouterSpeedTest(string devices, string? accountid, string? host, string? testtype);
        public Task<bool> GetDataRouterUptimeState(string devices);
        public Task<List<MultiChartDto>> GetGenericMultiChartValue(MultiChartGenericParamModel model);
        public Task<MongoPagedList<dynamic>> SearchMeasurement(FilterMeasurementModel model);
    }
    public class MeasurementServices : IMeasurementServices
    {
        private readonly IMeasurementRepository _measurementRepository;
        private readonly IGenericMeasurementRepository _genericMeasurementRepository;
        private readonly ITenantSettingRepository _tenantSettingRepository;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;
        private readonly Tenant _tenantService;
        private readonly IHubContext<IotHub> _iotHubContext;
        private readonly IMongoClient _mongoClient;
        private readonly IOperationRepository _operationRepository;
        private readonly IDeviceRepository _deviceRepository;
        private readonly ISmsSender _smsSender;
        private readonly IEmailSender _emailSender;
        private readonly SendGridOptions _options;
        private readonly RestSharpOptions _restOptions;
        private readonly IDeviceStatusService _deviceStatusService;
        private readonly IRepositoryFactory _repositoryFactory;
        private readonly HttpClient _client;
        private readonly IWidgetRepository _widgetRepository;
        private readonly IDeviceService _deviceService; // David Lim 09112023

        public MeasurementServices(
            IMeasurementRepository measurementRepository,
            IGenericMeasurementRepository genericMeasurementRepository,
            ITenantSettingRepository tenantSettingRepository,
            IMapper mapper,
            ILogger logger,
            IMultiTenantService<Common.Mongodb.MultiTenant.Tenant> tenantService,
            IHubContext<IotHub> iotHubContext,
            IMongoClient mongoClient,
            IOperationRepository operationRepository,
            IDeviceRepository deviceRepository,
            ISmsSender smsSender,
            IEmailSender emailSender,
            SendGridOptions options,
            RestSharpOptions restSharpOptions,
            IDeviceStatusService deviceStatusService,
            IRepositoryFactory repositoryFactory,
            HttpClient client,
            IWidgetRepository widgetRepository,
            IDeviceService deviceService // David Lim 09112023
            )
        {
            this._measurementRepository = measurementRepository ?? throw new ArgumentNullException(nameof(measurementRepository));
            this._genericMeasurementRepository = genericMeasurementRepository ?? throw new ArgumentNullException(nameof(genericMeasurementRepository));
            this._tenantSettingRepository = tenantSettingRepository ?? throw new ArgumentNullException(nameof(tenantSettingRepository));
            this._mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._tenantService = tenantService.GetTenantAsync().Result ?? throw new TenantNotFoundException("Can't find tenant infomation when handle measurement!");
            this._iotHubContext = iotHubContext ?? throw new ArgumentNullException(nameof(iotHubContext));
            this._mongoClient = mongoClient ?? throw new ArgumentNullException(nameof(mongoClient));
            this._operationRepository = operationRepository ?? throw new ArgumentNullException(nameof(operationRepository));
            this._deviceRepository = deviceRepository ?? throw new ArgumentNullException(nameof(deviceRepository));
            this._smsSender = smsSender ?? throw new ArgumentNullException(nameof(smsSender));
            this._emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
            this._options = options ?? throw new ArgumentNullException(nameof(options));
            this._restOptions = restSharpOptions ?? throw new ArgumentNullException(nameof(restSharpOptions));
            this._deviceStatusService = deviceStatusService ?? throw new ArgumentNullException(nameof(deviceStatusService));
            this._repositoryFactory = repositoryFactory ?? throw new ArgumentNullException(nameof(repositoryFactory));
            _client = client;
            _widgetRepository = widgetRepository;
            this._deviceService = deviceService ?? throw new ArgumentNullException(nameof(deviceService)); // David Lim 09112023
        }

        public async Task<dynamic> Create(CreateMeasurementModel model)
        {
            var measurementRepo = _repositoryFactory.GetRepository<BsonDocument>("Measurement");
            var entity = BsonDocument.Parse(model.Payload);
            //var db = _mongoClient.GetDatabase(_tenantService.Database);
            //var meaCollection = db.GetCollection<BsonDocument>("Measurement");
            //checking logic payload
            var listJsonElement = BsonDocument.Parse(model.Payload).Elements.ToList();
            if (listJsonElement[0].Name.Equals("measurements"))
            {
                var lstChild = listJsonElement[0].Value.AsBsonArray;
                foreach (var child in lstChild)
                {
                    entity = child.ToBsonDocument();
                    // Check Threshold 
                    await CheckThresholdSetting(entity);
                    entity["time"] = DateTime.Parse(entity["time"].ToString());
                    //await meaCollection.InsertOneAsync(entity); // this change to: await measurementRepo.InsertAsync(entity)
                    await measurementRepo.InsertAsync(entity);
                    //try to update device status
                    await TryUpdateDeviceStatus(entity);
                    await TryUpdateDeviceStatusFromIotData(entity);
                }

                var payloadEntity = entity.ToDynamicRaw();
                // send signalr for measurement change for each item
                await TrySendSignalr(payloadEntity);
                entity["_id"] = entity["_id"].ToString();
                entity["time"] = entity["time"].ToString();
                return entity.ToDynamicRaw();
            }
            // Check Threshold 
            await CheckThresholdSetting(entity);
            entity["time"] = DateTime.Parse(entity["time"].ToString());
            //await meaCollection.InsertOneAsync(entity); change this to:  await measurementRepo.InsertAsync(entity);
            await measurementRepo.InsertAsync(entity);

            //try to update device status
            await TryUpdateDeviceStatus(entity);
            await TryUpdateDeviceStatusFromIotData(entity);

            var payload = entity.ToDynamicRaw();

            // send signalr for measurement change
            await TrySendSignalr(payload);

            entity["_id"] = entity["_id"].ToString();
            entity["time"] = entity["time"].ToString();

            return entity.ToDynamicRaw();
        }

        private async Task CheckThresholdSetting(
            BsonDocument entity
            //IMongoDatabase db
            )
        {
            // Check Threshold 
            //var tenantSettingCollection = db.GetCollection<BsonDocument>("TenantSetting");
            var tenantSettingRepo = _repositoryFactory.GetRepository<BsonDocument>("TenantSetting");

            //var alarmCollection = db.GetCollection<Alarm>("Alarm");
            var alarmRepo = _repositoryFactory.GetRepository<Alarm>("Alarm");

            //var lstRuls = tenantSettingCollection.AsQueryable().ToList().Where(i => i.Elements.Any(x => x.Value == "Threshold"));
            var lstRuls = tenantSettingRepo.Collection.AsQueryable().ToList().Where(i => i.Elements.Any(x => x.Value == "Threshold"));

            var matchingRule = lstRuls.Where(x => x["Value"]["Device"].IsBsonNull || x["Value"]["Device"].ToString().ToLower().Equals(entity["source"]["id"].ToString().ToLower()));
            var lstAction = new List<BsonDocument>();

            foreach (var rule in matchingRule)
            {
                switch (rule["Value"]["Operator"].ToInt32())
                {
                    case (int)Operator.range:
                        if (rule["Value"]["ValueFrom"].ToDouble() < GetValueByCriteria(rule["Value"]["Criteria"].ToString(), entity) && GetValueByCriteria(rule["Value"]["Criteria"].ToString(), entity) < rule["Value"]["ValueTo"].ToDouble()) lstAction.Add(rule);
                        break;
                    case (int)Operator.range_equal:
                        if (rule["Value"]["ValueFrom"].ToDouble() <= GetValueByCriteria(rule["Value"]["Criteria"].ToString(), entity) && GetValueByCriteria(rule["Value"]["Criteria"].ToString(), entity) <= rule["Value"]["ValueTo"].ToDouble()) lstAction.Add(rule);
                        break;
                    case (int)Operator.eq:
                        if (rule["Value"]["ValueEqual"].ToString().Equals(GetValueEqualCriteria(rule["Value"]["Criteria"].ToString(), entity)))
                            lstAction.Add(rule);
                        break;
                    case (int)Operator.onvaluechange:
                        var oldvalue = await GetGenericSingleValue(new GenericParamModel() { SourceId = entity["source"]["id"].ToString(), DataPath = rule["Value"]["Criteria"].ToString(), DataPoint = entity["type"].ToString() });
                        if (!rule["Value"]["ValueEqual"].ToString().ToLower().Equals(oldvalue.Value?.ToString().ToLower())) lstAction.Add(rule);
                        break;
                }
            }

            //process Action
            foreach (var action in lstAction)
            {
                var actionItems = action["Value"]["Actions"].AsBsonArray;
                var lstActionsUpdate = new BsonArray();
                var siTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
                DateTime siTime = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.Local, siTimeZone);

                foreach (var item in actionItems)
                {
                    if ((int)item["Type"] == (int)ActionType.SMS)
                    {
                        var lastTime = item.ToBsonDocument().GetValue("LastTrigger", null);
                        var frequency = item.ToBsonDocument().GetValue("Frequency", null);
                        if (frequency.RawValue == null || (frequency.RawValue != null && (lastTime.RawValue == null || Convert.ToDateTime(lastTime).AddMinutes(frequency.ToDouble()) < DateTime.UtcNow)))
                        {
                            //var lstRecipientSetting = tenantSettingCollection.AsQueryable().ToList().Where(i => i.Elements.Any(x => x.Value == "AlarmRecepient"));
                            var lstRecipientSetting = tenantSettingRepo.Collection.AsQueryable().ToList().Where(i => i.Elements.Any(x => x.Value == "AlarmRecepient"));

                            var recipientSetting = lstRecipientSetting.FirstOrDefault(x => x["Value"]["ListName"].ToString().ToLower().Equals(item["Recipient"].ToString().ToLower()));

                            if (recipientSetting != null)
                            {
                                var lstRecipient = new List<string>();
                                lstRecipient = recipientSetting["Value"]["Recepient"].AsBsonArray.Select(x => x.ToString()).ToList();
                                await _smsSender.SendSmsAsync(lstRecipient, "Device " + entity["source"]["id"].ToString() + " " + item["Description"].ToString() + " at: " + siTime.ToString());//DateTime.UtcNow + " UTC");
                            }
                            else
                            {
                                var contacts = item["Recipient"].ToString().Split(",").ToList().Where(x => VerifyPhone(x)).ToList();
                                if (contacts.Any())
                                {
                                    await _smsSender.SendSmsAsync(contacts, "Device " + entity["source"]["id"].ToString() + " " + item["Description"].ToString() + " at: " + siTime.ToString()); //DateTime.UtcNow + " UTC");
                                }

                            }
                        }
                        if (lastTime != null)
                        {
                            item.ToBsonDocument().Set("LastTrigger", DateTime.UtcNow);
                        }
                        else
                        {
                            item.ToBsonDocument().Add("LastTrigger", DateTime.UtcNow);
                        }
                        lstActionsUpdate.Add(item);
                    }
                    if ((int)item["Type"] == (int)ActionType.Email)
                    {
                        //code for SMTP
                        //var lstRecipientSetting = tenantSettingCollection.AsQueryable().ToList().Where(i => i.Elements.Any(x => x.Value == "AlarmRecepient"));
                        //var recipientSetting = lstRecipientSetting.FirstOrDefault(x => x["Value"]["ListName"].ToString().ToLower().Equals(item["Recipient"].ToString().ToLower()));
                        //if (recipientSetting != null)
                        //{
                        //    var lstRecipient = new List<string>();
                        //    lstRecipient = recipientSetting["Value"]["Recepient"].AsBsonArray.Select(x => x.ToString()).ToList();
                        //    await _emailSender.SendAsync(lstRecipient, "Alarm Notification for " + entity["source"]["id"].ToString(), "Device " + entity["source"]["id"].ToString() + " " + item["Description"].ToString() + " at: " + DateTime.UtcNow + " UTC");
                        //}

                        //Send email using SendGrid
                        //var lstRecipientSetting = tenantSettingCollection.AsQueryable().ToList().Where(i => i.Elements.Any(x => x.Value == "AlarmRecepient"));
                        var lastTime = item.ToBsonDocument().GetValue("LastTrigger", null);
                        var frequency = item.ToBsonDocument().GetValue("Frequency", null);
                        if (frequency.RawValue == null || (frequency.RawValue != null && (lastTime.RawValue == null || Convert.ToDateTime(lastTime).AddMinutes(frequency.ToDouble()) < DateTime.UtcNow)))
                        {
                            var lstRecipientSetting = tenantSettingRepo.Collection.AsQueryable().ToList().Where(i => i.Elements.Any(x => x.Value == "AlarmRecepient"));

                            var recipientSetting = lstRecipientSetting.FirstOrDefault(x => x["Value"]["ListName"].ToString().ToLower().Equals(item["Recipient"].ToString().ToLower()));
                            if (recipientSetting != null)
                            {
                                var lstRecipient = new List<string>();
                                lstRecipient = recipientSetting["Value"]["Recepient"].AsBsonArray.Select(x => x.ToString()).ToList();
                                var client = new SendGridClient(_options.SendGridAPIKey);
                                var msg = new SendGridMessage()
                                {
                                    From = new EmailAddress(_options.Sender),
                                    Subject = "Alarm Notification for " + entity["source"]["id"].ToString(),
                                    // HtmlContent = "Device " + entity["source"]["id"].ToString() + " " + item["Description"].ToString() + " at: " + DateTime.UtcNow + " UTC"
                                    // 16739 he datetime in the content of email notifications are in UtC. It should be translated to local time to avoid confusion.   
                                    HtmlContent = "Device " + entity["source"]["id"].ToString() + " " + item["Description"].ToString() + " at: " + siTime.ToString() //DateTime.Now
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
                                var contact = item["Recipient"].ToString().Split(",");
                                var client = new SendGridClient(_options.SendGridAPIKey);
                                var msg = new SendGridMessage()
                                {
                                    From = new EmailAddress(_options.Sender),
                                    Subject = "Alarm Notification for " + entity["source"]["id"].ToString(),
                                    //HtmlContent = "Device " + entity["source"]["id"].ToString() + " " + item["Description"].ToString() + " at: " + DateTime.UtcNow + " UTC"
                                    // 16739 he datetime in the content of email notifications are in UtC. It should be translated to local time to avoid confusion.   
                                    HtmlContent = "Device " + entity["source"]["id"].ToString() + " " + item["Description"].ToString() + " at: " + siTime.ToString() //DateTime.Now
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
                                    //var response = await client.SendEmailAsync(msg);
                                    await _emailSender.SendAsync(contact.ToList(), msg.Subject, msg.HtmlContent);
                                }
                            }
                        }
                        if (lastTime != null)
                        {
                            item.ToBsonDocument().Set("LastTrigger", DateTime.UtcNow);
                        }
                        else
                        {
                            item.ToBsonDocument().Add("LastTrigger", DateTime.UtcNow);
                        }
                        lstActionsUpdate.Add(item);
                    }
                    if ((int)item["Type"] == (int)ActionType.Alarm)
                    {
                        var lastTime = item.ToBsonDocument().GetValue("LastTrigger", null);
                        var frequency = item.ToBsonDocument().GetValue("Frequency", null);

                        if (frequency.RawValue == null || (frequency.RawValue != null && (lastTime.RawValue == null || Convert.ToDateTime(lastTime).AddMinutes(frequency.ToDouble()) < DateTime.UtcNow)))

                        // if (frequency.RawValue == null || (frequency.RawValue != null && (lastTime.RawValue == null || ((DateTime)lastTime.RawValue).AddMinutes(frequency.ToDouble()) < DateTime.UtcNow)))


                        {
                            var device = _deviceRepository.GetFirstOrDefault(x => x.SourceId.Equals(entity["source"]["id"].ToString()) && (x.Status == null || x.Status == DeviceConstants.ActiveDevice));
                            var alarm = new Alarm();
                            alarm.CreatedDate = DateTime.UtcNow;
                            alarm.Source = new DeviceAlarm()
                            {
                                Name = device != null ? device.Name : String.Empty,
                                Id = entity["source"]["id"].ToString()
                            };
                            alarm.Type = item["AlarmType"].ToString();
                            alarm.Text = item["Description"].IsBsonNull ? String.Empty : item["Description"].ToString();
                            alarm.Severity = ((SeverityType)item["SeverityType"].AsInt32).ToString();
                            alarm.Status = AlarmConstants.Active;
                            alarm.Time = DateTime.UtcNow;
                            await alarmRepo.InsertAsync(alarm);
                        }


                        await SendMeasurementSignalChange(string.Empty, entity["source"]["id"].ToString());
                        if (lastTime != null)
                        {
                            item.ToBsonDocument().Set("LastTrigger", DateTime.UtcNow);
                        }
                        else
                        {
                            item.ToBsonDocument().Add("LastTrigger", DateTime.UtcNow);
                        }
                        lstActionsUpdate.Add(item);
                    }
                }
                if (lstActionsUpdate.Count > 0)
                {
                    action["Value"]["Actions"] = lstActionsUpdate;
                    await tenantSettingRepo.UpdateAsync(action, x => x["_id"] == action["_id"], default);
                }
            }
        }

        private static bool VerifyEmail(string input)
        {
            // Define the regex patterns for email and phone number
            string emailPattern = @"^[^@\s] +@[^@\s\.]+\.[^@\.\s]+$";

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
        private double? GetValueByCriteria(string criteria, BsonDocument entity)
        {
            var struc = criteria.Split('.');
            entity = BsonDocument.Parse(entity.ToJson().ToLower());
            BsonValue result;
            var match = entity.TryGetValue(struc[0].ToLower(), out result);
            foreach (var kvp in struc)
            {
                if (kvp != struc[0])
                {
                    if (match)
                    {
                        match = result.AsBsonDocument.TryGetValue(kvp.ToLower(), out result);
                    }
                }
            }
            if (result != null)
            {
                return result.ToDouble();
            }
            return null;
        }

        private string? GetValueEqualCriteria(string criteria, BsonDocument entity)
        {
            try
            {
                var struc = criteria.Split('.');
                entity = BsonDocument.Parse(entity.ToJson().ToLower());
                BsonValue result;
                var match = entity.TryGetValue(struc[0].ToLower(), out result);
                foreach (var kvp in struc)
                {
                    if (kvp != struc[0])
                    {
                        if (match)
                        {
                            match = result.AsBsonDocument.TryGetValue(kvp.ToLower(), out result);
                        }

                    }
                }
                if (result != null)
                {
                    return result.ToString();
                }
                return null;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        private async Task SendMeasurementSignalChange(string? panelId, string? deviceId)
        {
            if (panelId != null && deviceId != null)
            {
                await _iotHubContext.Clients
                   .Group(IotHub.IotHubGroup + _tenantService.Name)
                   .SendAsync(IotHub.MeasurementChange,
                               new MeasurementSignalrModel
                               {
                                   DeviceId = deviceId,
                                   PanelId = panelId
                               }
                   );
            }
        }

        private bool DoesPropertyExist(dynamic settings, string name)
        {
            if (settings is ExpandoObject)
                return ((IDictionary<string, object>)settings).ContainsKey(name);

            return settings.GetType().GetProperty(name) != null;
        }


        private async Task TrySendSignalr(dynamic payload)
        {
            var sourceId = string.Empty;
            if (DoesPropertyExist(payload, "source"))
            {
                sourceId = payload.source?.id;
            }

            if (!string.IsNullOrEmpty(sourceId))
            {
                await SendMeasurementSignalChange("Panel-01", sourceId);
            }

        }

        private async Task TryUpdateDeviceStatus(BsonDocument entity)
        {
            try
            {
                var payload = entity.ToDynamic();

                var isDeviceRelay = DoesPropertyExist(payload, "mcxRelayStatus");

                if (!isDeviceRelay) return;

                var mcxRelayStatus = payload?.mcxRelayStatus;

                if (mcxRelayStatus != null)
                {
                    var commandNumber = (string)mcxRelayStatus.cno;
                    var pointStatus = (string)mcxRelayStatus.pointstatus;

                    var commandEntity = (await _operationRepository.FindAsyncWithoutDataAuthorize(x => x.CNO == commandNumber)).FirstOrDefault();

                    if (commandEntity != null)
                    {
                        // update command status
                        commandEntity.PointStatus = pointStatus;
                        commandEntity.UpdatedDate = DateTime.UtcNow;
                        commandEntity.CommandStatus = OperationConstants.OperationProcessed;

                        await _operationRepository.UpdateAsync(commandEntity, x => commandEntity.Id == x.Id);

                        // update device status
                        var deviceEntity = (await _deviceRepository.FindAsyncWithoutDataAuthorize(x => x.SourceId == commandEntity.Source && (x.Status == null || x.Status == DeviceConstants.ActiveDevice))).FirstOrDefault();

                        if (deviceEntity != null)
                        {
                            deviceEntity.UpdatedDate = DateTime.UtcNow;
                            deviceEntity.OperationStatus = pointStatus;

                            await _deviceRepository.UpdateAsync(deviceEntity, x => x.Id == deviceEntity.Id);
                        }
                        else
                        {
                            throw new Exception("Can't find device when update device status or maybe the device are inactive!");
                        }

                    }
                    else
                    {
                        throw new Exception("Can't find command when update device status!");
                    }

                }
                else
                {
                    // ignore if the payload is not mxc_replay
                    return;
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        private async Task TryUpdateDeviceStatusFromIotData(BsonDocument entity)
        {
            try
            {
                var payload = entity.ToDynamic();

                var typeOfPayload = (string)payload?.type;

                if (typeOfPayload == null || string.IsNullOrEmpty(typeOfPayload)) return;

                switch (typeOfPayload)
                {
                    case "5G Device Router":
                        {

                            var currentStates = (string)payload.Net_5GRouter.state.value;
                            var currentDeviceId = (string)payload.source.id;

                            if (currentStates == null) return;

                            // update device status
                            var deviceEntity = (await _deviceRepository.FindAsyncWithoutDataAuthorize(x => x.SourceId == currentDeviceId && (x.Status == null || x.Status == DeviceConstants.ActiveDevice))).FirstOrDefault();

                            if (deviceEntity != null)
                            {
                                deviceEntity.OperationStatus = currentStates == "online" ? OperationConstants.StatusOn : OperationConstants.StatusOff;
                                deviceEntity.UpdatedDate = DateTime.UtcNow;

                                await _deviceRepository.UpdateAsync(deviceEntity, x => x.Id == deviceEntity.Id);
                            }
                            else
                            {
                                throw new Exception("Can't find device when update device status or maybe the device are inactive!");
                            }

                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<List<dynamic>> GetAll()
        {
            //var db = _mongoClient.GetDatabase(_tenantService.Database);
            //var meaCollection = db.GetCollection<dynamic>("Measurement");
            var measurementRepo = _repositoryFactory.GetRepository<dynamic>("Measurement");
            //var result = meaCollection.AsQueryable().ToList();
            var result = measurementRepo.GetAllWithDataAuthorize().ToList();

            //var response = _mapper.Map<List<MeasurementDto>>(result);
            return result;
        }


        public async Task<List<dynamic>> SearchForWatsons(FilterMeasurementModel model)
        {
            var filterById = Builders<Measurement>.Filter.Empty;

            if (model.Id != null)
            {
                filterById = Builders<Measurement>.Filter.Eq("Payload.Source._id", model.Id);
            }

            var filterByStartDate = Builders<Measurement>.Filter.Gt("Payload.Time", model.StartDate);

            var filterByEndDate = Builders<Measurement>.Filter.Lt("Payload.Time", model.EndDate);

            var filter = Builders<Measurement>.Filter.And(filterByStartDate, filterByEndDate, filterById);

            var respones = await _measurementRepository.FindBuilderAsync<Measurement>(filter);
            var data = new List<WatsonsMeasurementDto>();

            if (model.Periodicity == DataConstants.Periodicity.daily)
            {
                data = respones.GroupBy(x => x.Payload.Time.Date).Select(x => new WatsonsMeasurementDto()
                {
                    Time = x.First().Payload.Time,
                    Channel1 = new Mcx_pv()
                    {
                        Current = new Data()
                        {
                            Unit = "A",
                            Value = x.Average(i => i.Payload.Channel1.Current.Value)
                        },
                        Voltage = new Data()
                        {
                            Unit = "V",
                            Value = x.Average(i => i.Payload.Channel1.Voltage.Value)
                        },
                        Power = new Data()
                        {
                            Unit = "kWh",
                            Value = x.Sum((i) => i.Payload.Channel1.Power.Value)
                        }
                    },
                    Channel2 = new Mcx_pv()
                    {
                        Current = new Data()
                        {
                            Unit = "A",
                            Value = x.Average(i => i.Payload.Channel2.Current.Value)
                        },
                        Voltage = new Data()
                        {
                            Unit = "V",
                            Value = x.Average(i => i.Payload.Channel2.Voltage.Value)
                        },
                        Power = new Data()
                        {
                            Unit = "kWh",
                            Value = x.Sum((i) => i.Payload.Channel2.Power.Value)
                        }
                    },
                    Channel3 = new Mcx_pv()
                    {
                        Current = new Data()
                        {
                            Unit = "A",
                            Value = x.Average(i => i.Payload.Channel3.Current.Value)
                        },
                        Voltage = new Data()
                        {
                            Unit = "V",
                            Value = x.Average(i => i.Payload.Channel3.Voltage.Value)
                        },
                        Power = new Data()
                        {
                            Unit = "kWh",
                            Value = x.Sum((i) => i.Payload.Channel3.Power.Value)
                        }
                    },
                    Channel = new Mcx_pv()
                    {
                        Current = new Data()
                        {
                            Unit = "A",
                            Value = (x.Average(i => i.Payload.Channel1.Current.Value) + x.Average(i => i.Payload.Channel2.Current.Value) + x.Average(i => i.Payload.Channel3.Current.Value)) / 3
                        },
                        Voltage = new Data()
                        {
                            Unit = "V",
                            Value = (x.Average(i => i.Payload.Channel1.Voltage.Value) + x.Average(i => i.Payload.Channel2.Voltage.Value) + x.Average(i => i.Payload.Channel3.Voltage.Value)) / 3
                        },
                        Power = new Data()
                        {
                            Unit = "kWh",
                            Value = x.Sum(i => (i.Payload.Channel1.Power.Value + i.Payload.Channel2.Power.Value + i.Payload.Channel3.Power.Value))
                        }
                    }

                }).ToList();
            }
            if (model.Periodicity == DataConstants.Periodicity.weekly)
            {
                data = respones.GroupBy(x => x.Payload.Time.Date.AddDays(-(int)x.Payload.Time.Date.DayOfWeek)).Select(x => new WatsonsMeasurementDto()
                {
                    Time = x.First().Payload.Time,
                    Channel1 = new Mcx_pv()
                    {
                        Current = new Data()
                        {
                            Unit = "A",
                            Value = x.Average(i => i.Payload.Channel1.Current.Value)
                        },
                        Voltage = new Data()
                        {
                            Unit = "V",
                            Value = x.Average(i => i.Payload.Channel1.Voltage.Value)
                        },
                        Power = new Data()
                        {
                            Unit = "kWh",
                            Value = x.Sum((i) => i.Payload.Channel1.Power.Value)
                        }
                    },
                    Channel2 = new Mcx_pv()
                    {
                        Current = new Data()
                        {
                            Unit = "A",
                            Value = x.Average(i => i.Payload.Channel2.Current.Value)
                        },
                        Voltage = new Data()
                        {
                            Unit = "V",
                            Value = x.Average(i => i.Payload.Channel2.Voltage.Value)
                        },
                        Power = new Data()
                        {
                            Unit = "kWh",
                            Value = x.Sum((i) => i.Payload.Channel2.Power.Value)
                        }
                    },
                    Channel3 = new Mcx_pv()
                    {
                        Current = new Data()
                        {
                            Unit = "A",
                            Value = x.Average(i => i.Payload.Channel3.Current.Value)
                        },
                        Voltage = new Data()
                        {
                            Unit = "V",
                            Value = x.Average(i => i.Payload.Channel3.Voltage.Value)
                        },
                        Power = new Data()
                        {
                            Unit = "kWh",
                            Value = x.Sum((i) => i.Payload.Channel3.Power.Value)
                        }
                    },
                    Channel = new Mcx_pv()
                    {
                        Current = new Data()
                        {
                            Unit = "A",
                            Value = (x.Average(i => i.Payload.Channel1.Current.Value) + x.Average(i => i.Payload.Channel2.Current.Value) + x.Average(i => i.Payload.Channel3.Current.Value)) / 3
                        },
                        Voltage = new Data()
                        {
                            Unit = "V",
                            Value = (x.Average(i => i.Payload.Channel1.Voltage.Value) + x.Average(i => i.Payload.Channel2.Voltage.Value) + x.Average(i => i.Payload.Channel3.Voltage.Value)) / 3
                        },
                        Power = new Data()
                        {
                            Unit = "kWh",
                            Value = x.Sum(i => (i.Payload.Channel1.Power.Value + i.Payload.Channel2.Power.Value + i.Payload.Channel3.Power.Value))
                        }
                    }


                }).ToList();
            }
            if (model.Periodicity == DataConstants.Periodicity.monthly)
            {
                data = respones.GroupBy(x => new { x.Payload.Time.Year, x.Payload.Time.Month }).Select(x => new WatsonsMeasurementDto()
                {
                    Time = x.First().Payload.Time,
                    Channel1 = new Mcx_pv()
                    {
                        Current = new Data()
                        {
                            Unit = "A",
                            Value = x.Average(i => i.Payload.Channel1.Current.Value)
                        },
                        Voltage = new Data()
                        {
                            Unit = "V",
                            Value = x.Average(i => i.Payload.Channel1.Voltage.Value)
                        },
                        Power = new Data()
                        {
                            Unit = "kWh",
                            Value = x.Sum((i) => i.Payload.Channel1.Power.Value)
                        }
                    },
                    Channel2 = new Mcx_pv()
                    {
                        Current = new Data()
                        {
                            Unit = "A",
                            Value = x.Average(i => i.Payload.Channel2.Current.Value)
                        },
                        Voltage = new Data()
                        {
                            Unit = "V",
                            Value = x.Average(i => i.Payload.Channel2.Voltage.Value)
                        },
                        Power = new Data()
                        {
                            Unit = "kWh",
                            Value = x.Sum((i) => i.Payload.Channel2.Power.Value)
                        }
                    },
                    Channel3 = new Mcx_pv()
                    {
                        Current = new Data()
                        {
                            Unit = "A",
                            Value = x.Average(i => i.Payload.Channel3.Current.Value)
                        },
                        Voltage = new Data()
                        {
                            Unit = "V",
                            Value = x.Average(i => i.Payload.Channel3.Voltage.Value)
                        },
                        Power = new Data()
                        {
                            Unit = "kWh",
                            Value = x.Sum((i) => i.Payload.Channel3.Power.Value)
                        }
                    },
                    Channel = new Mcx_pv()
                    {
                        Current = new Data()
                        {
                            Unit = "A",
                            Value = (x.Average(i => i.Payload.Channel1.Current.Value) + x.Average(i => i.Payload.Channel2.Current.Value) + x.Average(i => i.Payload.Channel3.Current.Value)) / 3
                        },
                        Voltage = new Data()
                        {
                            Unit = "V",
                            Value = (x.Average(i => i.Payload.Channel1.Voltage.Value) + x.Average(i => i.Payload.Channel2.Voltage.Value) + x.Average(i => i.Payload.Channel3.Voltage.Value)) / 3
                        },
                        Power = new Data()
                        {
                            Unit = "kWh",
                            Value = x.Sum(i => (i.Payload.Channel1.Power.Value + i.Payload.Channel2.Power.Value + i.Payload.Channel3.Power.Value))
                        }
                    }


                }).ToList();
            }
            if (model.Periodicity == DataConstants.Periodicity.yearly)
            {
                data = respones.GroupBy(x => x.Payload.Time.Date.Year).Select(x => new WatsonsMeasurementDto()
                {
                    Time = x.First().Payload.Time,
                    Channel1 = new Mcx_pv()
                    {
                        Current = new Data()
                        {
                            Unit = "A",
                            Value = x.Average(i => i.Payload.Channel1.Current.Value)
                        },
                        Voltage = new Data()
                        {
                            Unit = "V",
                            Value = x.Average(i => i.Payload.Channel1.Voltage.Value)
                        },
                        Power = new Data()
                        {
                            Unit = "kWh",
                            Value = x.Sum((i) => i.Payload.Channel1.Power.Value)
                        }
                    },
                    Channel2 = new Mcx_pv()
                    {
                        Current = new Data()
                        {
                            Unit = "A",
                            Value = x.Average(i => i.Payload.Channel2.Current.Value)
                        },
                        Voltage = new Data()
                        {
                            Unit = "V",
                            Value = x.Average(i => i.Payload.Channel2.Voltage.Value)
                        },
                        Power = new Data()
                        {
                            Unit = "kWh",
                            Value = x.Sum((i) => i.Payload.Channel2.Power.Value)
                        }
                    },
                    Channel3 = new Mcx_pv()
                    {
                        Current = new Data()
                        {
                            Unit = "A",
                            Value = x.Average(i => i.Payload.Channel3.Current.Value)
                        },
                        Voltage = new Data()
                        {
                            Unit = "V",
                            Value = x.Average(i => i.Payload.Channel3.Voltage.Value)
                        },
                        Power = new Data()
                        {
                            Unit = "kWh",
                            Value = x.Sum((i) => i.Payload.Channel3.Power.Value)
                        }
                    },
                    Channel = new Mcx_pv()
                    {
                        Current = new Data()
                        {
                            Unit = "A",
                            Value = (x.Average(i => i.Payload.Channel1.Current.Value) + x.Average(i => i.Payload.Channel2.Current.Value) + x.Average(i => i.Payload.Channel3.Current.Value)) / 3
                        },
                        Voltage = new Data()
                        {
                            Unit = "V",
                            Value = (x.Average(i => i.Payload.Channel1.Voltage.Value) + x.Average(i => i.Payload.Channel2.Voltage.Value) + x.Average(i => i.Payload.Channel3.Voltage.Value)) / 3
                        },
                        Power = new Data()
                        {
                            Unit = "kWh",
                            Value = x.Sum(i => (i.Payload.Channel1.Power.Value + i.Payload.Channel2.Power.Value + i.Payload.Channel3.Power.Value))
                        }
                    }


                }).ToList();
            }

            var result = _mapper.Map<List<dynamic>>(data);
            return result;
        }

        public async Task<bool> Update(string id, UpdateMeasurementModel model)
        {
            //var db = _mongoClient.GetDatabase(_tenantService.Database);
            //var meaCollection = db.GetCollection<BsonDocument>("Measurement");
            var measurementRespo = _repositoryFactory.GetRepository<BsonDocument>("Measurement");
            var entity = BsonDocument.Parse(model.Payload);
            entity["time"] = DateTime.Parse(entity["time"].ToString());

            FilterDefinition<BsonDocument> filter = new BsonDocument("_id", new ObjectId(id));
            try
            {
                await measurementRespo.UpdateAsync(entity, filter, default(CancellationToken));
                return true;
            }
            catch (Exception)
            {
                throw;
            }
        }


        public async Task<bool> Delete(string id)
        {
            // var db = _mongoClient.GetDatabase(_tenantService.Database);
            //var meaCollection = db.GetCollection<BsonDocument>("Measurement");
            var measurementRespo = _repositoryFactory.GetRepository<BsonDocument>("Measurement");

            FilterDefinition<BsonDocument> filter = new BsonDocument("_id", new ObjectId(id));

            await measurementRespo.DeleteAsync(filter, default(CancellationToken));
            return true;
        }

        public async Task<TotalPowerDto> GetGenericPower(string? id, string? subId, string? tenant, DataConstants.Periodicity? periodicity)
        {
            var tenantSetting = _tenantSettingRepository.GetFirstOrDefault();

            var StartDate = DateTime.Now.AddDays(-1);
            var EndDate = DateTime.Now;


            switch (periodicity)
            {
                case DataConstants.Periodicity.daily:
                    StartDate = DateTime.Today;
                    break;
                case DataConstants.Periodicity.weekly:
                    StartDate = DateTime.Now.StartOfWeek(DayOfWeek.Monday);

                    break;
                case DataConstants.Periodicity.monthly:
                    StartDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                    break;
                case DataConstants.Periodicity.yearly:
                    StartDate = new DateTime(DateTime.Now.Year, 1, 1);

                    break;
            }
            if (string.IsNullOrEmpty(id))
            {
                StartDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            }
            var listActiveDevice = await _deviceStatusService.GetActiveDevice(new List<string>());


            CheckDeviceAndReturnDefault(id);

            PipelineDefinition<BsonDocument, GenericPowerDto> pipeline = new BsonDocument[]
                        {
                                new BsonDocument("$match",
                                new BsonDocument
                                    {
                                        { "time",
                                            new BsonDocument
                                                    {
                                                        { "$gte",
                                            StartDate },
                                                        { "$lte",
                                            EndDate }
                                                    } },
                                        { "type", "gtech_Power" },
                                        { "source.id", string.IsNullOrEmpty(id) ? new BsonDocument("$ne", BsonNull.Value) : id },
                                        { "$or",
                                        new BsonArray
                                                {
                                                    new BsonDocument("source.subId",
                                                    new BsonDocument("$exists", false)),
                                                    new BsonDocument("source.subId", string.IsNullOrEmpty(subId) ? new BsonDocument("$ne", BsonNull.Value) : subId)
                                                } }
                                    }),
                                new BsonDocument("$sort",
                                new BsonDocument("time", -1)),
                                new BsonDocument("$limit", 1),
                                new BsonDocument("$project",
                                new BsonDocument
                                    {
                                        { "TotalPower",
                                            new BsonDocument("$multiply",
                                            new BsonArray
                                                        {
                                                            "$gtech_power.current.value",
                                                            "$gtech_power.voltage.value"
                                                        }) },
                                        { "AvgVoltage", "$gtech_power.voltage.value" },
                                        { "AvgCurrent", "$gtech_power.current.value" },
                                        { "CurrentPotentialSaving", 0 },
                                        { "TotalPotentialSaving", 0 },
                                        { "Time", "$time" }
                                    })
                        };

            if (!string.IsNullOrEmpty(tenant) && tenant.Equals("nus"))
            {
                pipeline = new BsonDocument[]
                {
                    new BsonDocument("$match",
                        new BsonDocument
                            {
                                { "$or",
                        new BsonArray
                                {
                                    new BsonDocument("type", "mcx_PV"),
                                    new BsonDocument("type", "mcx_Battery")
                                } },
                                { "time",
                                            new BsonDocument
                                                    {
                                                        { "$gte",
                                            StartDate },
                                                        { "$lte",
                                            EndDate }
                                                    } },
                                { "source.id", string.IsNullOrEmpty(id) ? new BsonDocument("$ne", BsonNull.Value) : id }
                            }),
                        new BsonDocument("$group",
                        new BsonDocument
                            {
                                { "_id", "$time" },
                                { "docs",
                        new BsonDocument("$push", "$$ROOT") }
                            }),
                        new BsonDocument("$match",
                        new BsonDocument("docs",
                        new BsonDocument("$all",
                        new BsonArray
                                    {
                                        new BsonDocument("$elemMatch",
                                        new BsonDocument("type", "mcx_PV")),
                                        new BsonDocument("$elemMatch",
                                        new BsonDocument("type", "mcx_Battery"))
                                    }))),
                        new BsonDocument("$project",
                        new BsonDocument
                            {
                                { "_id", 0 },
                                { "mcx_PV",
                        new BsonDocument("$arrayElemAt",
                        new BsonArray
                                    {
                                        new BsonDocument("$filter",
                                        new BsonDocument
                                            {
                                                { "input", "$docs" },
                                                { "as", "doc" },
                                                { "cond",
                                        new BsonDocument("$eq",
                                        new BsonArray
                                                    {
                                                        "$$doc.type",
                                                        "mcx_PV"
                                                    }) }
                                            }),
                                        0
                                    }) },
                                { "mcx_Battery",
                        new BsonDocument("$arrayElemAt",
                        new BsonArray
                                    {
                                        new BsonDocument("$filter",
                                        new BsonDocument
                                            {
                                                { "input", "$docs" },
                                                { "as", "doc" },
                                                { "cond",
                                        new BsonDocument("$eq",
                                        new BsonArray
                                                    {
                                                        "$$doc.type",
                                                        "mcx_Battery"
                                                    }) }
                                            }),
                                        0
                                    }) },
                                { "time", "$_id" },
                                { "source",
                        new BsonDocument("$arrayElemAt",
                        new BsonArray
                                    {
                                        new BsonDocument("$filter",
                                        new BsonDocument
                                            {
                                                { "input", "$docs" },
                                                { "as", "doc" },
                                                { "cond",
                                        new BsonDocument("$eq",
                                        new BsonArray
                                                    {
                                                        "$$doc.type",
                                                        "mcx_PV"
                                                    }) }
                                            }),
                                        0
                                    }) }
                            }),
                        new BsonDocument("$project",
                        new BsonDocument
                            {
                                { "mcx_PV", "$mcx_PV.mcx_PV" },
                                { "mcx_Battery", "$mcx_Battery.mcx_Battery" },
                                { "time", "$time" },
                                { "source", "$source.source" }
                            }),
                        new BsonDocument("$sort",
                        new BsonDocument("time", -1)),
                        new BsonDocument("$limit", 1),
                        new BsonDocument("$project",
                        new BsonDocument
                            {
                                { "TotalPower",
                        new BsonDocument("$multiply",
                        new BsonArray
                                    {
                                        "$mcx_PV.current.value",
                                        "$mcx_Battery.voltage.value"
                                    }) },
                                { "AvgVoltage", "$mcx_PV.voltage.value" },
                                { "AvgCurrent", "$mcx_PV.current.value" },
                                { "CurrentPotentialSaving", 0 },
                                { "TotalPotentialSaving", 0 },
                                { "Time", "$time" }
                            })
                };
            }


            //var db = _mongoClient.GetDatabase(_tenantService.Database);
            //var meaCollection = db.GetCollection<dynamic>("Measurement");
            var measurementRespo = _repositoryFactory.GetRepository<BsonDocument>("Measurement");

            //var data = await meaCollection.AggregateAsync<GenericPowerDto>(pipeline);
            var data = await measurementRespo.Collection.AggregateAsync<GenericPowerDto>(pipeline);

            var result = data.ToList();

            var respone = new TotalPowerDto();

            if (result.Any())

            {
                try
                {
                    respone = new TotalPowerDto()
                    {
                        TotalPower = Math.Round((double)result.Sum(x => x.TotalPower), 2),
                        CurrentPotentialSaving = 0,
                        TotalPotentialSaving = 0,
                        AvgVoltage = result.Any(x => x.AvgVoltage != null) ? Math.Round((double)result.Average(x => x.AvgVoltage), 2) : 0,
                        AvgCurrent = result.Any(x => x.AvgCurrent != null) ? Math.Round((double)result.Average(x => x.AvgCurrent), 2) : 0,
                        Time = result.Any() ? result.First().Time : null
                    };
                }
                catch (Exception ex)
                {

                }

            }

            return respone;
        }

        public async Task<TotalPowerDto> GetPowerForWatsons(string? id, IEnumerable<string> channelNames, DataConstants.Periodicity? periodicity)
        {
            var StartDate = DateTime.UtcNow.AddDays(-1);
            var EndDate = DateTime.UtcNow;


            switch (periodicity)
            {
                case DataConstants.Periodicity.daily:
                    StartDate = DateTime.Today;
                    break;
                case DataConstants.Periodicity.weekly:
                    StartDate = DateTime.Now.StartOfWeek(DayOfWeek.Monday);

                    break;
                case DataConstants.Periodicity.monthly:
                    StartDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                    break;
                case DataConstants.Periodicity.yearly:
                    StartDate = new DateTime(DateTime.Now.Year, 1, 1);

                    break;
            }



            PipelineDefinition<dynamic, PowerDto> pipeline = new BsonDocument[]
            {

                new BsonDocument("$match",
                new BsonDocument("Type", "mcx_PV")),
                new BsonDocument("$addFields",
                new BsonDocument("Time",
                new BsonDocument("$toDate", "$Time"))),
                new BsonDocument("$match",
                new BsonDocument("Time",
                new BsonDocument("$gt",
                StartDate))),
                new BsonDocument("$match",
                new BsonDocument("Time",
                new BsonDocument("$lt",
                EndDate))),
                new BsonDocument("$group",
                new BsonDocument
                    {
                        { "_id", "$Source._id" },
                        { "TotalPowerChannel1",
                new BsonDocument("$sum",
                new BsonDocument("$toDouble", "$Channel1.Power.Value")) },
                        { "TotalPowerChannel2",
                new BsonDocument("$sum",
                new BsonDocument("$toDouble", "$Channel2.Power.Value")) },
                        { "TotalPowerChannel3",
                new BsonDocument("$sum",
                new BsonDocument("$toDouble", "$Channel3.Power.Value")) },
                        { "AvgCurrentChannel1",
                new BsonDocument("$avg",
                new BsonDocument("$toDouble", "$Channel1.Current.Value")) },
                        { "AvgCurrentChannel2",
                new BsonDocument("$avg",
                new BsonDocument("$toDouble", "$Channel2.Current.Value")) },
                        { "AvgCurrentChannel3",
                new BsonDocument("$avg",
                new BsonDocument("$toDouble", "$Channel3.Current.Value")) },
                        { "AvgVoltageChannel1",
                new BsonDocument("$avg",
                new BsonDocument("$toDouble", "$Channel1.Voltage.Value")) },
                        { "AvgVoltageChannel2",
                new BsonDocument("$avg",
                new BsonDocument("$toDouble", "$Channel2.Voltage.Value")) },
                        { "AvgVoltageChannel3",
                new BsonDocument("$avg",
                new BsonDocument("$toDouble", "$Channel3.Voltage.Value")) }
                    })
            };

            //var db = _mongoClient.GetDatabase(_tenantService.Database);
            //var meaCollection = db.GetCollection<dynamic>("Measurement");
            //var data = await meaCollection.AggregateAsync<PowerDto>(pipeline);
            var measurementRespo = _repositoryFactory.GetRepository<dynamic>("Measurement");
            var data = await measurementRespo.Collection.AggregateAsync<PowerDto>(pipeline);

            var listData = data.ToList();

            if (id != null)
            {
                listData = listData.Where(x => x._id == id).ToList();
            }

            double sumPower = 0.0;
            double avgCurrent = 0.0;
            double avgVoltage = 0.0;

            if (listData.Count > 0)
            {

                if (channelNames != null && channelNames.Contains("Channel1"))
                {
                    sumPower = sumPower + (listData.Sum(x => x.TotalPowerChannel1));
                    avgCurrent = avgCurrent + listData.Average(x => x.AvgCurrentChannel1);
                    avgVoltage = avgVoltage + listData.Average(x => x.AvgVoltageChannel1);
                }

                if (channelNames != null && channelNames.Contains("Channel2"))
                {
                    sumPower = sumPower + (listData.Sum(x => x.TotalPowerChannel2));
                    avgCurrent = avgCurrent + listData.Average(x => x.AvgCurrentChannel2);
                    avgVoltage = avgVoltage + listData.Average(x => x.AvgVoltageChannel2);
                }

                if (channelNames != null && channelNames.Contains("Channel3"))
                {
                    sumPower = sumPower + (listData.Sum(x => x.TotalPowerChannel3));
                    avgCurrent = avgCurrent + listData.Average(x => x.AvgCurrentChannel3);
                    avgVoltage = avgVoltage + listData.Average(x => x.AvgVoltageChannel3);
                }

                if (channelNames == null)
                {
                    sumPower = sumPower + (listData.Sum(x => x.TotalPowerChannel1) +
                                           listData.Sum(x => x.TotalPowerChannel2) +
                                           listData.Sum(x => x.TotalPowerChannel3));
                    avgCurrent = avgCurrent + (listData.Average(x => x.AvgCurrentChannel1) +
                                               listData.Average(x => x.AvgCurrentChannel2) +
                                               listData.Average(x => x.AvgCurrentChannel3)) / 3;
                    avgVoltage = avgVoltage + (listData.Average(x => x.AvgVoltageChannel1) +
                                               listData.Average(x => x.AvgVoltageChannel2) +
                                               listData.Average(x => x.AvgVoltageChannel3)) / 3;
                }
            }


            return new TotalPowerDto()
            {
                TotalPower = Math.Round(sumPower, 2, MidpointRounding.AwayFromZero),
                AvgCurrent = Math.Round(avgCurrent, 2, MidpointRounding.AwayFromZero),
                AvgVoltage = Math.Round(avgVoltage, 2, MidpointRounding.AwayFromZero),

            };
        }

        public async Task<List<dynamic>> SearchGeneric(FilterMeasurementModel model)
        {

            PipelineDefinition<dynamic, BsonDocument> pipeline = new BsonDocument[]
            {
                new BsonDocument("$match",
                new BsonDocument("time",
                new BsonDocument
                        {
                            { "$gte",
                model.StartDate },
                            { "$lte",
                model.EndDate }
            }))
            };

            if (!string.IsNullOrEmpty(model.Id))
            {
                CheckDeviceAndReturnDefault(model.Id);

                pipeline = new BsonDocument[]
                {
                    new BsonDocument("$match",
                    new BsonDocument
                        {
                            { "time",
                    new BsonDocument
                            {
                                { "$gte",
                    model.StartDate },
                                { "$lte",
                    model.EndDate }
                            } },
                            { "source.id", model.Id }
                        })
                };
            }

            //var db = _mongoClient.GetDatabase(_tenantService.Database);
            //var meaCollection = db.GetCollection<dynamic>("Measurement");
            var measurementRespo = _repositoryFactory.GetRepository<dynamic>("Measurement");

            //var data = await meaCollection.AggregateAsync<BsonDocument>(pipeline);
            var data = await measurementRespo.ExecuteAggregateAsync<BsonDocument>(pipeline, "source.id");

            var response = data.ToList().Select(x => x.ToDynamicRaw()).ToList();

            return response;
        }

        public async Task<MongoPagedList<dynamic>> SearchMeasurement(FilterMeasurementModel model)
        {
            var listFilter = new List<BsonDocument>();

            var dateMatch = new BsonDocument("$match",
                new BsonDocument("time",
                new BsonDocument
                        {
                            { "$gte",
                model.StartDate },
                            { "$lte",
                model.EndDate }
            }));

            listFilter.Add(dateMatch);

            if (!string.IsNullOrEmpty(model.Id))
            {
                CheckDeviceAndReturnDefault(model.Id);

                var sourceMath =
                    new BsonDocument("$match",
                    new BsonDocument
                        {
                            { "source.id", model.Id }
                        });

                listFilter.Add(sourceMath);
            }


            var faceMAtch = new BsonDocument("$facet",
    new BsonDocument
        {
            { "items",
    new BsonArray
            {
                new BsonDocument("$skip", model.PageSize * (model.PageIndex-1)),
                new BsonDocument("$limit", model.PageSize)
            } },
            { "totalCount",
    new BsonArray
            {
                new BsonDocument("$count", "count")
            } }
        });

            var pagedListProject = new BsonDocument("$project",
        new BsonDocument
            {
            { "Items", "$items" },
            { "TotalPage",
    new BsonDocument("$ceil",
    new BsonDocument("$divide",
    new BsonArray
                    {
                        new BsonDocument("$arrayElemAt",
                        new BsonArray
                            {
                                "$totalCount.count",
                                0
                            }),
                        model.PageSize
                    })) }
            });


            listFilter.Add(faceMAtch);
            listFilter.Add(pagedListProject);

            PipelineDefinition<BsonDocument, MongoPagedList<dynamic>> pipeline = listFilter.ToArray();

            //var db = _mongoClient.GetDatabase(_tenantService.Database);
            //var meaCollection = db.GetCollection<dynamic>("Measurement");
            var measurementRespo = _repositoryFactory.GetRepository<BsonDocument>("Measurement");

            //var data = await meaCollection.AggregateAsync<BsonDocument>(pipeline);
            var data = await measurementRespo.Collection.AggregateAsync<MongoPagedList<dynamic>>(pipeline);

            var response = data.ToList().FirstOrDefault();

            return response;
        }

        public async Task<List<string>> ExportGeneric(FilterMeasurementModel model)
        {
            var listActiveDevice = await _deviceStatusService.GetActiveDevice(new List<string>());

            PipelineDefinition<dynamic, BsonDocument> pipeline = new BsonDocument[]
            {
                 new BsonDocument("$match",
                    new BsonDocument("source.id",
                    new BsonDocument("$in",
                    new BsonArray(listActiveDevice)

                    ))),
                new BsonDocument("$match",
                new BsonDocument("time",
                new BsonDocument
                        {
                            { "$gte",
                model.StartDate.HasValue ? model.StartDate.Value.AddHours(-8) : model.StartDate },
                            { "$lte",
                model.EndDate }
            }))
            };

            if (!string.IsNullOrEmpty(model.Id))
            {
                CheckDeviceAndReturnDefault(model.Id);

                pipeline = new BsonDocument[]
                {
                    new BsonDocument("$match",
                    new BsonDocument
                        {
                            { "time",
                    new BsonDocument
                            {
                                { "$gte",
                    model.StartDate },
                                { "$lte",
                    model.EndDate }
                            } },
                            { "source.id", model.Id }
                        })
                };
            }
            if (!string.IsNullOrEmpty(model.Id) && !string.IsNullOrEmpty(model.DataType))
            {
                pipeline = new BsonDocument[]
                {
                    new BsonDocument("$match",
                    new BsonDocument
                        {
                            { "time",
                    new BsonDocument
                            {
                                { "$gte",
                    model.StartDate },
                                { "$lte",
                    model.EndDate }
                            } },
                            { "source.id", model.Id },
                            new BsonDocument("type",
                            new BsonDocument("$in",
                            new BsonArray(model.DataType.Split(",").Select(i => i.Trim()))))
                        })
                };
            }
            //var db = _mongoClient.GetDatabase(_tenantService.Database);
            //var meaCollection = db.GetCollection<dynamic>("Measurement");
            var measurementRespo = _repositoryFactory.GetRepository<dynamic>("Measurement");
            var data = await measurementRespo.ExecuteAggregateAsync<BsonDocument>(pipeline);

            var response = data.ToList().ConvertAll(x => BsonTypeMapper.MapToDotNetValue(x)).ToList();

            return response.Select(x => JsonConvert.SerializeObject(x)).ToList();
        }
        public async Task<DeviceBatteryDto> GetGenericBatteryIndicator(string id)
        {
            CheckDeviceAndReturnDefault(id);

            //var StartDate = DateTime.Now.AddDays(-1);
            var StartDate = DateTime.Now.AddMinutes(-9);    //fetch data starting from less than 10minutes ago
            var EndDate = DateTime.Now;

            PipelineDefinition<BsonDocument, BatteryDto> pipeline = new BsonDocument[]
            {
                new BsonDocument("$match",
                new BsonDocument
                    {
                        { "source.id",
                new BsonDocument("$eq", id) },
                        { "type",
                new BsonDocument("$in",
                new BsonArray
                            {
                                "mcx_PV",
                                "mcx_Battery"
                            }) }
                    }),
                new BsonDocument("$sort",
                new BsonDocument("time", -1)),
                new BsonDocument("$limit", 1),
                new BsonDocument("$group",
                new BsonDocument
                    {
                        { "_id", "$type" },
                        { "TotalPower",
                new BsonDocument("$sum",
                new BsonDocument("$cond",
                new BsonArray
                                {
                                    new BsonDocument("$eq",
                                    new BsonArray
                                        {
                                            "$type",
                                            "mcx_PV"
                                        }),
                                    new BsonDocument("$divide",
                                    new BsonArray
                                        {
                                            new BsonDocument("$multiply",
                                            new BsonArray
                                                {
                                                    "$mcx_PV.voltage.value",
                                                    "$mcx_PV.current.value"
                                                }),
                                            6
                                        }),
                                    new BsonDocument("$divide",
                                    new BsonArray
                                        {
                                            new BsonDocument("$multiply",
                                            new BsonArray
                                                {
                                                    "$mcx_Battery.voltage.value",
                                                    "$mcx_Battery.current.value"
                                                }),
                                            6
                                        })
                                })) }
                    })
            };

            //var db = _mongoClient.GetDatabase(_tenantService.Database);
            //var meaCollection = db.GetCollection<dynamic>("Measurement");

            var measurementRespo = _repositoryFactory.GetRepository<BsonDocument>("Measurement");

            //var data = await meaCollection.AggregateAsync<BatteryDto>(pipeline);
            var data = await measurementRespo.Collection.AggregateAsync<BatteryDto>(pipeline);

            //get Voltage data
            PipelineDefinition<BsonDocument, VoltageDto> pipelineVoltage = new BsonDocument[]
            {
                new BsonDocument("$match",
                new BsonDocument
                    {
                    //fixed 17021 
                        { "source.id",
                new BsonDocument("$eq", id) },
                        { "time",
                new BsonDocument
                        {
                            { "$gt",
                StartDate },
                            { "$lt",
                EndDate }
                        } },
                        { "type", "mcx_Battery" },
                        { "mcx_Battery",
                new BsonDocument("$exists", true) }
                    }),
                new BsonDocument("$sort",
                new BsonDocument("time", -1)),
                new BsonDocument("$limit", 1),
                new BsonDocument("$project",
                new BsonDocument
                    {
                        { "Time", "$time" },
                        { "Voltage", "$mcx_Battery.voltage.value" }
                    })
            };

            //var dataVoltage = await meaCollection.AggregateAsync<VoltageDto>(pipelineVoltage);

            var dataVoltage = await measurementRespo.Collection.AggregateAsync<VoltageDto>(pipelineVoltage);

            var totalPower = data.ToList();
            var currentVoltage = dataVoltage.ToList().OrderByDescending(x => x.Time).ToList();

            var totalPowerGenerated = totalPower.Any(x => x._id.Equals("mcx_PV")) ? Math.Round((Double)totalPower.FirstOrDefault(x => x._id.Equals("mcx_PV")).TotalPower, 2) : 0;
            var batteryTotalConsumed = totalPower.Any(x => x._id.Equals("mcx_Battery")) ? Math.Round((Double)totalPower.FirstOrDefault(x => x._id.Equals("mcx_Battery")).TotalPower, 2) : 0;
            var batteryCurrentVoltage = (currentVoltage != null && currentVoltage.Any() && currentVoltage.FirstOrDefault().Voltage != null) ? Math.Round((Double)currentVoltage.FirstOrDefault().Voltage, 2) : 0;
            DateTime? dateTime = (currentVoltage != null && currentVoltage.Any() && currentVoltage.FirstOrDefault().Time != null) ? currentVoltage.FirstOrDefault().Time : null;

            // Fixed isssue get data within 1 hour
            TimeSpan ts = dateTime.HasValue ? DateTime.UtcNow - dateTime.Value : TimeSpan.FromMinutes(0);
            if (ts.TotalMinutes > 10)
            {
                batteryTotalConsumed = 0;
                dateTime = DateTime.Now;
            }

       
            var result = new DeviceBatteryDto()
            {
                DeviceId = id,
                TotalPowerGenerated = totalPowerGenerated,
                BatteryTotalConsumed = batteryTotalConsumed,
                BatteryCurrentVoltage = batteryCurrentVoltage,
                DataTime = dateTime
            };

            return result;
        }

        public async Task<TemperatureDto> GetTemperatureIndicator(string id)
        {
            CheckDeviceAndReturnDefault(id);

            var StartDate = DateTime.Now.AddDays(-1);
            var EndDate = DateTime.Now;
            //get Temperature data
            PipelineDefinition<BsonDocument, TemperatureRawDto> pipelineTemperature = new BsonDocument[]
            {
                new BsonDocument("$match",
                new BsonDocument
                    {
                        { "time",
                new BsonDocument
                        {
                            { "$gte",
                StartDate },
                            { "$lte",
                EndDate }
                        } },
                        { "source.id",
                new BsonDocument("$eq", id) },
                        { "type", "mcx_Temperature" }
                    }),
                new BsonDocument("$project",
                new BsonDocument
                    {
                        { "Time", "$time" },
                        { "Enclosure", "$mcx_Temperature.External.value" },
                        { "Solar", "$mcx_Temperature.Internal.value" }
                    })
            };
            //var db = _mongoClient.GetDatabase(_tenantService.Database);
            //var meaCollection = db.GetCollection<dynamic>("Measurement");
            var measurementRespo = _repositoryFactory.GetRepository<BsonDocument>("Measurement");

            //var dataTemperature = await meaCollection.AggregateAsync<TemperatureRawDto>(pipelineTemperature);
            var dataTemperature = await measurementRespo.Collection.AggregateAsync<TemperatureRawDto>(pipelineTemperature);


            var currentTemperature = dataTemperature.ToList().OrderByDescending(x => x.Time);

            var result = new TemperatureDto()
            {
                Deviceid = id,
                Enclosure = currentTemperature.Any() ? currentTemperature.FirstOrDefault().Enclosure : 0,
                Solar = currentTemperature.Any() ? currentTemperature.FirstOrDefault().Solar : 0,
                Time = currentTemperature.Any() ? currentTemperature.FirstOrDefault().Time : DateTime.UtcNow
            };

            return result;
        }
        public async Task<FiveGDto> GetFiveGIndicator(string id)
        {
            CheckDeviceAndReturnDefault(id);

            var StartDate = DateTime.Now.AddDays(-1);
            var EndDate = DateTime.Now;
            //get Upload data
            PipelineDefinition<BsonDocument, UploadDto> pipeline5GUpload = new BsonDocument[]
            {
                new BsonDocument("$match",
                new BsonDocument
                    {
                        { "time",
                new BsonDocument
                        {
                            { "$gte",
                StartDate },
                            { "$lte",
                EndDate }
                        } },
                        { "source.id",
                new BsonDocument("$eq", id) },
                        { "type", "5G Device Speed Upload" }
                    }),
                new BsonDocument("$sort",
                new BsonDocument("time", -1)),
                new BsonDocument("$limit", 1),
                new BsonDocument("$project",
                new BsonDocument
                    {
                        { "Time", "$time" },
                        { "Speed", "$Net_5GDeviceUpload.throughput.value" }
                    })
            };
            //var db = _mongoClient.GetDatabase(_tenantService.Database);
            //var meaCollection = db.GetCollection<dynamic>("Measurement");
            var measurementRespo = _repositoryFactory.GetRepository<BsonDocument>("Measurement");

            // David Lim 09112023
            var deviceDto = await _deviceService.GetDeviceSource(id);

            var data5gUpload = await measurementRespo.Collection.AggregateAsync<UploadDto>(pipeline5GUpload);

            PipelineDefinition<BsonDocument, DownloadDto> pipeline5GDownload = new BsonDocument[]
            {
                new BsonDocument("$match",
                new BsonDocument
                    {
                        { "time",
                new BsonDocument
                        {
                            { "$gte",
                StartDate },
                            { "$lte",
                EndDate }
                        } },
                        { "source.id",
                new BsonDocument("$eq", id) },
                        { "type", "5G Device Speed Download" }
                    }),
                new BsonDocument("$sort",
                new BsonDocument("time", -1)),
                new BsonDocument("$limit", 1),
                new BsonDocument("$project",
                new BsonDocument
                    {
                        { "Time", "$time" },
                        { "Speed", "$Net_5GDeviceDownload.throughput.value" }
                    })
            };

            var data5gDownload = await measurementRespo.Collection.AggregateAsync<DownloadDto>(pipeline5GDownload);

            PipelineDefinition<BsonDocument, LatencyDto> pipeline5GLatency = new BsonDocument[]
            {
                new BsonDocument("$match",
                new BsonDocument
                    {
                        { "time",
                new BsonDocument
                        {
                            { "$gte",
                StartDate },
                            { "$lte",
                EndDate }
                        } },
                        { "source.id",
                new BsonDocument("$eq", id) },
                        { "type", "5G Device Speed Latency" }
                    }),
                new BsonDocument("$sort",
                new BsonDocument("time", -1)),
                new BsonDocument("$limit", 1),
                new BsonDocument("$project",
                new BsonDocument
                    {
                        { "Time", "$time" },
                        { "Latency", "$Net_5GDeviceLatency.latency.value" }
                    })
            };
            var data5gLatency = await measurementRespo.Collection.AggregateAsync<LatencyDto>(pipeline5GLatency);

            PipelineDefinition<BsonDocument, SignalDto> pipeline5GSignal = new BsonDocument[]
{
                new BsonDocument("$match",
                new BsonDocument
                    {
                        { "time",
                new BsonDocument
                        {
                            { "$gte",
                StartDate },
                            { "$lte",
                EndDate }
                        } },
                        { "source.id",
                new BsonDocument("$eq", id) },
                        { "type", "5G Device Signal" }
                    }),
                new BsonDocument("$sort",
                new BsonDocument("time", -1)),
                new BsonDocument("$limit", 1),
                new BsonDocument("$project",
                new BsonDocument
                    {
                        { "Time", "$time" },
                        { "SignalPercent", "$Net_5GSignal.signal_percent.value" }
                    })
};
            var data5gSignal = await measurementRespo.Collection.AggregateAsync<SignalDto>(pipeline5GSignal);

            var currentUpload = data5gUpload.ToList().OrderByDescending(x => x.Time);
            var currentDownload = data5gDownload.ToList().OrderByDescending(x => x.Time);
            var currentLatency = data5gLatency.ToList().OrderByDescending(x => x.Time);
            var currentSinal = data5gSignal.ToList().OrderByDescending(x => x.Time);

            var result = new FiveGDto()
            {
                Deviceid = id,
                //SignalDatetime = currentSinal.Any() ? currentSinal.FirstOrDefault().Time : deviceDto.UpdatedDate, // David Lim 09112023
                SignalDatetime = deviceDto.UpdatedDate,
                UploadSpeed = currentUpload.Any() ? Math.Round(currentUpload.FirstOrDefault().Speed.Value, 2, MidpointRounding.AwayFromZero) : 0,
                DownloadSpeed = currentDownload.Any() ? Math.Round(currentDownload.FirstOrDefault().Speed.Value, 2, MidpointRounding.AwayFromZero) : 0,
                Latency = currentLatency.Any() ? Math.Round(currentLatency.FirstOrDefault().Latency.Value / 1000, 2, MidpointRounding.AwayFromZero) : 0,
                SignalPercent = (currentSinal.Any() && deviceDto.OperationStatus != "Off") ? currentSinal.FirstOrDefault().SignalPercent : null,
                Rsrp5g = currentSinal.Any() ? currentSinal.FirstOrDefault().Rsrp5g : 0
            };

            return result;
        }
        public async Task<bool> GetDataRouterStatus(string devices)
        {
            var measurementRepo = _repositoryFactory.GetRepository<RouterStatus>("Measurement");
            var client = new RestClient(_restOptions.Endpoint);

            var lstDevice = devices.Split(",");
            foreach (var deviceId in lstDevice)
            {
                var device = _deviceRepository.GetFirstOrDefault(x => x.SourceId.Equals(deviceId));
                if (device.Ext_Info != null)
                {
                    var queryCrad = new RestRequest("routers/" + device.Ext_Info.RouterId);
                    queryCrad.AddHeader("X-ECM-API-ID", _restOptions.EcmApiId)
                    .AddHeader("X-ECM-API-KEY", _restOptions.EcmApiKey)
                    .AddHeader("X-CP-API-ID", _restOptions.CpApiId)
                    .AddHeader("X-CP-API-KEY", _restOptions.CpApiKey);

                    var results = client.Get<CradleRouterDto>(queryCrad);
                    if (results != null)
                    {
                        var item = new RouterStatus()
                        {
                            source = new Source
                            {
                                id = deviceId,
                            },
                            time = DateTime.UtcNow,
                            type = "5G Device Router",
                            Net_5GRouter = new DeviceRouter
                            {
                                state = new DeviceRouterData { value = results.state },
                                name = new DeviceRouterData { value = results.name },
                                updated_at = results.state_updated_at
                            }
                        };
                        //--fix for 16979, 17314
                        //await measurementRepo.InsertAsync(item);
                        var model = new CreateMeasurementModel()
                        {
                            Payload = System.Text.Json.JsonSerializer.Serialize(item)
                        };
                        await this.Create(model);
                    }
                }
            }
            return true;

        }

        public async Task<bool> GetDataRouterSignal(string devices)
        {
            var measurementRepo = _repositoryFactory.GetRepository<RouterSignal>("Measurement");
            var client = new RestClient(_restOptions.Endpoint);

            var lstDevice = devices.Split(",");
            foreach (var deviceId in lstDevice)
            {
                var hasData = true;
                var lastItem = await GetLastGenericSingleValueData(new GenericParamModel { SourceId = deviceId, DataPoint = "5G Device Signal", DataPath = "Net_5GSignal.uptime.value" });
                var device = _deviceRepository.GetFirstOrDefault(x => x.SourceId.Equals(deviceId));
                if (device.Ext_Info != null && device.Ext_Info.InterfaceId != null && device.Ext_Info.InterfaceId.Any())
                {
                    var queryCrad = new RestRequest("net_device_signal_samples/");
                    queryCrad.AddHeader("X-ECM-API-ID", _restOptions.EcmApiId)
                        .AddHeader("X-ECM-API-KEY", _restOptions.EcmApiKey)
                        .AddHeader("X-CP-API-ID", _restOptions.CpApiId)
                        .AddHeader("X-CP-API-KEY", _restOptions.CpApiKey);
                    queryCrad.AddParameter("net_device", device.Ext_Info.InterfaceId.First())
                        .AddParameter("limit", 10);

                    do
                    {
                        if (lastItem != null)
                        {
                            //set filter
                            queryCrad.AddOrUpdateParameter("created_at_timeuuid__gt", lastItem.Id);
                        }
                        var results = client.Get<CradleDataResult>(queryCrad);
                        if (results != null && results.data.Any())
                        {
                            foreach (var result in results.data)
                            {
                                var item = new RouterSignal()
                                {
                                    source = new DeviceSource
                                    {
                                        id = deviceId,
                                    },
                                    time = result.created_at,
                                    type = "5G Device Signal",
                                    created_at_timeuuid = result.created_at_timeuuid,
                                    Net_5GSignal = new DeviceSignal
                                    {
                                        rsrp5g = new Device5GData { value = result.rsrp5g.HasValue ? result.rsrp5g : result.rsrp },
                                        rsrq5g = new Device5GData { value = result.rsrq5g.HasValue ? result.rsrq5g : result.rsrq },
                                        sinr5g = new Device5GData { value = result.sinr5g.HasValue ? result.sinr5g : result.sinr },
                                        signal_percent = new Device5GData { value = result.signal_percent },
                                        uptime = new Device5GData { value = result.uptime }
                                    }
                                };
                                //--fix for 16979, 17314
                                //await measurementRepo.InsertAsync(item);
                                var model = new CreateMeasurementModel()
                                {
                                    Payload = System.Text.Json.JsonSerializer.Serialize(item)
                                };
                                await this.Create(model);
                            }

                        }
                        if (results != null && string.IsNullOrEmpty(results.meta.next))
                        {
                            hasData = false;
                        }
                        else
                        {
                            queryCrad.AddOrUpdateParameter("created_at_timeuuid__lt", results.meta.next.Split("=").Last());
                        }
                    } while (hasData);

                }
            }
            return true;

        }
        //public async Task<bool> GetDataRouterSpeedTest(string devices, string? accountid, string? host, string? testtype)
        //{
        //    var measurementUploadRepo = _repositoryFactory.GetRepository<Router5GUpload>("Measurement");
        //    var measurementDownloadRepo = _repositoryFactory.GetRepository<Router5GDownload>("Measurement");
        //    var measurementLatencyRepo = _repositoryFactory.GetRepository<Router5GLatency>("Measurement");

        //    var t = new Thread(() => GetDataRouterSpeedTestTask(devices,  accountid,  host, testtype, measurementUploadRepo, measurementDownloadRepo, measurementLatencyRepo, _client));
        //    t.Start();
        //    return true;

        //}
        public async Task<bool> GetDataRouterUptimeState(string devices)
        {
            var measurementRepo = _repositoryFactory.GetRepository<RouterSignal>("Measurement");
            var client = new RestClient(_restOptions.Endpoint);

            var lstDevice = devices.Split(",");
            foreach (var deviceId in lstDevice)
            {
                var hasData = true;
                var lastItem = await GetLastGenericSingleValueData(new GenericParamModel { SourceId = deviceId, DataPoint = "5G Device Router Uptime", DataPath = "Net_5GRouterUptime.state.value" });
                var device = _deviceRepository.GetFirstOrDefault(x => x.SourceId.Equals(deviceId));
                if (device.Ext_Info != null && device.Ext_Info.InterfaceId != null && device.Ext_Info.InterfaceId.Any())
                {
                    var queryCrad = new RestRequest("router_state_samples/");
                    queryCrad.AddHeader("X-ECM-API-ID", _restOptions.EcmApiId)
                        .AddHeader("X-ECM-API-KEY", _restOptions.EcmApiKey)
                        .AddHeader("X-CP-API-ID", _restOptions.CpApiId)
                        .AddHeader("X-CP-API-KEY", _restOptions.CpApiKey);
                    queryCrad.AddParameter("router", device.Ext_Info.RouterId)
                        .AddParameter("limit", 100);

                    do
                    {
                        if (lastItem != null)
                        {
                            //set filter
                            queryCrad.AddOrUpdateParameter("created_at_timeuuid__gt", lastItem.Id);
                        }
                        var results = client.Get<CradleUptimeStateDataResult>(queryCrad);
                        if (results != null && results.data.Any())
                        {
                            foreach (var result in results.data)
                            {
                                var item = new RouterUptimeState()
                                    {
                                    time = result.created_at,
                                    created_at_timeuuid = result.created_at_timeuuid,
                                    type = "5G Device Router Uptime",
                                    source = new RouterUptimeState.SourceInfo
                                    {
                                        id = device.SourceId
                                    },
                                    Net_5GRouterUptime = new RouterUptimeState.Net5GRouterUpTime
                                    {
                                        period = new RouterUptimeState.Period
                                        {
                                            value = result.period
                                        },
                                        state = new RouterUptimeState.State
                                        {
                                            value = result.state
                                        }
                                    }
                                };

                                var model = new CreateMeasurementModel()
                                {
                                    Payload = System.Text.Json.JsonSerializer.Serialize(item)
                                };
                                await this.Create(model);
                            }
                        }
                        if (results != null && string.IsNullOrEmpty(results.meta.next))
                        {
                            hasData = false;
                        }
                        else
                        {
                            queryCrad.AddOrUpdateParameter("created_at_timeuuid__lt", results.meta.next.Split("=").Last());
                        }
                    } while (hasData);

                }
            }
            return true;

        }


        public async Task<bool> GetDataRouterSpeedTest(string devices, string? accountid, string? host, string? testtype)

        {
            if (string.IsNullOrEmpty(accountid))
            {
                accountid = "101274";
            }
            if (string.IsNullOrEmpty(host))
            {
                host = "20.24.141.255";
            }
            var lstTestType = new List<string>();
            if (!string.IsNullOrEmpty(testtype))
            {
                lstTestType.AddRange(testtype.Split(",").ToList());
            }
            else
            {
                lstTestType = new List<string>
                { "TCP Latency", "TCP Download", "TCP Upload" };
            }
            var measurementUploadRepo = _repositoryFactory.GetRepository<Router5GUpload>("Measurement");
            var measurementDownloadRepo = _repositoryFactory.GetRepository<Router5GDownload>("Measurement");
            var measurementLatencyRepo = _repositoryFactory.GetRepository<Router5GLatency>("Measurement");
            var client = new RestClient(_restOptions.Endpoint);

            var lstDevice = devices.Split(",");
            foreach (var deviceId in lstDevice)
            {
                var device = _deviceRepository.GetFirstOrDefault(x => x.SourceId.Equals(deviceId));
                if (device.Ext_Info != null && device.Ext_Info.InterfaceId != null && device.Ext_Info.InterfaceId.Any())
                {
                    //create test
                    var lstRequest = new List<CradleRouterTestDto>();
                    // Dung Tran

                    //string internalid = device.Ext_Info.InterfaceId.First();

                    //var latency = new Thread(() => LatencyTask(accountid, "TCP Latency", host, deviceId, internalid, client, measurementUploadRepo, measurementDownloadRepo, measurementLatencyRepo));
                    //latency.Start();
                    //var download = new Thread(() => DownloadTask(accountid, "TCP Download", host, deviceId, internalid, client, measurementUploadRepo, measurementDownloadRepo, measurementLatencyRepo));
                    //download.Start();
                    //var upload = new Thread(() => UploadTask(accountid, "TCP Upload", host, deviceId, internalid, client, measurementUploadRepo, measurementDownloadRepo, measurementLatencyRepo));
                    //upload.Start();

                    //latency.Join();
                    //download.Join();
                    //upload.Join();

                    //System.Threading.Thread.Sleep(18000);
                    //

                    foreach (var testType in lstTestType)
                    {
                        var retry = true;
                        var maxRetry = 10;
                        //var maxRetry = 5;
                        var retryCount = 0;
                        var content = new StringContent(JsonConvert.SerializeObject(new PostModel
                        {
                            account = "https://www.cradlepointecm.com/api/v2/accounts/" + accountid + "/",
                            config = new PostModelConfig
                            {
                                host = host,
                                max_test_concurrency = 5,
                                net_device_ids = new int[] { int.Parse(device.Ext_Info.InterfaceId.First()) },
                                port = 12865,
                                size = null,
                                test_timeout = 20,
                                test_type = testType,
                                time = 20
                            }
                        }), Encoding.UTF8, "application/json");
                        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                        content.Headers.Add("X-ECM-API-ID", _restOptions.EcmApiId);
                        content.Headers.Add("X-ECM-API-KEY", _restOptions.EcmApiKey);
                        content.Headers.Add("X-CP-API-ID", _restOptions.CpApiId);
                        content.Headers.Add("X-CP-API-KEY", _restOptions.CpApiKey);
                        HttpResponseMessage response;

                        try
                        {
                            response = await _client.PostAsync(_restOptions.Endpoint + "/speed_test/", content);

                            if (response.StatusCode != HttpStatusCode.Created)
                            {
                                _logger.Warn("Send message don't success: " + response.ReasonPhrase);
                                throw new SmsInvalidException("Send message don't success: " + response.ReasonPhrase);
                            }
                            else
                            {
                                var result = JsonConvert.DeserializeObject<CradleRouterTestDto>(response.Content.ReadAsStringAsync().Result);
                                if (result != null && !string.IsNullOrEmpty(result.id))
                                {
                                    //fetch test
                                    do
                                    {
                                        //Console.Write(retryCount);
                                        //System.Threading.Thread.Sleep(90000);
                                        // Fixed 16891
                                        System.Threading.Thread.Sleep(10000);
                                        var queryCradResult = new RestRequest("speed_test/" + result.id);
                                        queryCradResult.AddHeader("X-ECM-API-ID", _restOptions.EcmApiId)
                                        .AddHeader("X-ECM-API-KEY", _restOptions.EcmApiKey)
                                        .AddHeader("X-CP-API-ID", _restOptions.CpApiId)
                                        .AddHeader("X-CP-API-KEY", _restOptions.CpApiKey);
                                        try
                                        {
                                            var fetchresult = client.Get<CradleRouterTestResultDto>(queryCradResult);
                                            //insert to DB
                                            if (fetchresult != null)
                                            {
                                                if (fetchresult.results != null && fetchresult.results.Any())
                                                {
                                                    if (!fetchresult.results.First().state.Equals("error"))
                                                    {
                                                        if (testType.Equals("TCP Latency"))
                                                        {
                                                            var item = new Router5GLatency()
                                                            {
                                                                source = new DeviceSource
                                                                {
                                                                    id = deviceId,
                                                                },
                                                                time = DateTime.Parse(fetchresult.results.First().updated_at).ToUniversalTime(),
                                                                type = "5G Device Speed Latency",
                                                                netdeviceid = fetchresult.results.First().net_device_id.ToString(),
                                                                Net_5GDeviceLatency = new DeviceRouterLatency
                                                                {
                                                                    duration = new Device5GData { value = double.Parse(fetchresult.results.First().results.duration) },
                                                                    jitter = new Device5GData { value = double.Parse(fetchresult.results.First().results.jitter) },
                                                                    latency = new Device5GData { value = double.Parse(fetchresult.results.First().results.latency) }
                                                                }
                                                            };
                                                            //--fix for 16979
                                                            await measurementLatencyRepo.InsertAsync(item);
                                                            //var model = new CreateMeasurementModel()
                                                            //{
                                                            //    Payload = System.Text.Json.JsonSerializer.Serialize(item)
                                                            //};
                                                            //await this.Create(model);
                                                        }
                                                        if (testType.Equals("TCP Download"))
                                                        {
                                                            var item = new Router5GDownload()
                                                            {
                                                                source = new DeviceSource
                                                                {
                                                                    id = deviceId,
                                                                },
                                                                time = DateTime.Parse(fetchresult.results.First().updated_at).ToUniversalTime(),
                                                                type = "5G Device Speed Download",
                                                                netdeviceid = fetchresult.results.First().net_device_id.ToString(),
                                                                Net_5GDeviceDownload = new DeviceRouterDownload
                                                                {
                                                                    duration = new Device5GData { value = double.Parse(fetchresult.results.First().results.duration) },
                                                                    throughput = new Device5GData { value = double.Parse(fetchresult.results.First().results.throughput) },
                                                                    server_sent = new Device5GData { value = double.Parse(fetchresult.results.First().results.server_sent) }
                                                                }
                                                            };
                                                            //--fix for 16979
                                                            await measurementDownloadRepo.InsertAsync(item);
                                                            //var model = new CreateMeasurementModel()
                                                            //{
                                                            //    Payload = System.Text.Json.JsonSerializer.Serialize(item)
                                                            //};
                                                            //await this.Create(model);
                                                        }
                                                        if (testType.Equals("TCP Upload"))
                                                        {
                                                            var item = new Router5GUpload()
                                                            {
                                                                source = new DeviceSource
                                                                {
                                                                    id = deviceId,
                                                                },
                                                                time = DateTime.Parse(fetchresult.results.First().updated_at).ToUniversalTime(),
                                                                type = "5G Device Speed Upload",
                                                                netdeviceid = fetchresult.results.First().net_device_id.ToString(),
                                                                Net_5GDeviceUpload = new DeviceRouterUpload
                                                                {
                                                                    duration = new Device5GData { value = double.Parse(fetchresult.results.First().results.duration) },
                                                                    throughput = new Device5GData { value = double.Parse(fetchresult.results.First().results.throughput) },
                                                                    server_received = new Device5GData { value = double.Parse(fetchresult.results.First().results.server_sent) }
                                                                }
                                                            };
                                                            //--fix for 16979
                                                            await measurementUploadRepo.InsertAsync(item);
                                                            //var model = new CreateMeasurementModel()
                                                            //{
                                                            //    Payload = System.Text.Json.JsonSerializer.Serialize(item)
                                                            //};
                                                            //await this.Create(model);
                                                        }
                                                    }

                                                    retry = false;
                                                }
                                                else
                                                {
                                                    retryCount++;
                                                }
                                            }
                                            else
                                            {

                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            var fetchresulterror = client.Get<CradleRouterTestResultErrorDto>(queryCradResult);
                                            if (fetchresulterror != null && fetchresulterror.results != null && fetchresulterror.results.First().state.Equals("error"))
                                            {
                                                retry = false;
                                            }
                                        }


                                    } while (retry && retryCount < maxRetry);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            return false;
                        }
                    }

                }
            }
            return true;

        }

        private async Task<bool> LatencyTask(string accountid, string testType, string host, string deviceId, string internalid, RestClient client,
            IRepository<Router5GUpload> measurementUploadRepo, IRepository<Router5GDownload> measurementDownloadRepo, IRepository<Router5GLatency> measurementLatencyRepo)
        {
            //foreach (var testType in lstTestType)
            // {
            var retry = true;
            var maxRetry = 10;
            //var maxRetry = 5;
            var retryCount = 0;
            var content = new StringContent(JsonConvert.SerializeObject(new PostModel
            {
                account = "https://www.cradlepointecm.com/api/v2/accounts/" + accountid + "/",
                config = new PostModelConfig
                {
                    host = host,
                    max_test_concurrency = 5,
                    net_device_ids = new int[] { int.Parse(internalid) },
                    port = 12865,
                    size = null,
                    test_timeout = 20,
                    test_type = testType,
                    time = 20
                }
            }), Encoding.UTF8, "application/json");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            content.Headers.Add("X-ECM-API-ID", _restOptions.EcmApiId);
            content.Headers.Add("X-ECM-API-KEY", _restOptions.EcmApiKey);
            content.Headers.Add("X-CP-API-ID", _restOptions.CpApiId);
            content.Headers.Add("X-CP-API-KEY", _restOptions.CpApiKey);
            HttpResponseMessage response;

            try
            {
                response = await _client.PostAsync(_restOptions.Endpoint + "/speed_test/", content);

                if (response.StatusCode != HttpStatusCode.Created)
                {
                    _logger.Warn("Send message don't success: " + response.ReasonPhrase);
                    throw new SmsInvalidException("Send message don't success: " + response.ReasonPhrase);
                }
                else
                {
                    var result = JsonConvert.DeserializeObject<CradleRouterTestDto>(response.Content.ReadAsStringAsync().Result);
                    if (result != null && !string.IsNullOrEmpty(result.id))
                    {
                        //fetch test
                        do
                        {
                            Console.Write("LatencyTask:" + retryCount);
                            //System.Threading.Thread.Sleep(10000);
                            // Fixed 16891
                            System.Threading.Thread.Sleep(3000);
                            var queryCradResult = new RestRequest("speed_test/" + result.id);
                            queryCradResult.AddHeader("X-ECM-API-ID", _restOptions.EcmApiId)
                            .AddHeader("X-ECM-API-KEY", _restOptions.EcmApiKey)
                            .AddHeader("X-CP-API-ID", _restOptions.CpApiId)
                            .AddHeader("X-CP-API-KEY", _restOptions.CpApiKey);
                            try
                            {
                                var fetchresult = client.Get<CradleRouterTestResultDto>(queryCradResult);
                                //insert to DB
                                if (fetchresult != null)
                                {
                                    if (fetchresult.results != null && fetchresult.results.Any())
                                    {
                                        if (!fetchresult.results.First().state.Equals("error"))
                                        {
                                            //  if (testType.Equals("TCP Latency"))
                                            //  {
                                            var item = new Router5GLatency()
                                            {
                                                source = new DeviceSource
                                                {
                                                    id = deviceId,
                                                },
                                                time = DateTime.Parse(fetchresult.results.First().updated_at).ToUniversalTime(),
                                                type = "5G Device Speed Latency",
                                                netdeviceid = fetchresult.results.First().net_device_id.ToString(),
                                                Net_5GDeviceLatency = new DeviceRouterLatency
                                                {
                                                    duration = new Device5GData { value = double.Parse(fetchresult.results.First().results.duration) },
                                                    jitter = new Device5GData { value = double.Parse(fetchresult.results.First().results.jitter) },
                                                    latency = new Device5GData { value = double.Parse(fetchresult.results.First().results.latency) }
                                                }
                                            };
                                            await measurementLatencyRepo.InsertAsync(item);
                                            // }
                                            //if (testType.Equals("TCP Download"))
                                            //{
                                            //    var item = new Router5GDownload()
                                            //    {
                                            //        source = new DeviceSource
                                            //        {
                                            //            id = deviceId,
                                            //        },
                                            //        time = DateTime.Parse(fetchresult.results.First().updated_at).ToUniversalTime(),
                                            //        type = "5G Device Speed Download",
                                            //        netdeviceid = fetchresult.results.First().net_device_id.ToString(),
                                            //        Net_5GDeviceDownload = new DeviceRouterDownload
                                            //        {
                                            //            duration = new Device5GData { value = double.Parse(fetchresult.results.First().results.duration) },
                                            //            throughput = new Device5GData { value = double.Parse(fetchresult.results.First().results.throughput) },
                                            //            server_sent = new Device5GData { value = double.Parse(fetchresult.results.First().results.server_sent) }
                                            //        }
                                            //    };
                                            //    await measurementDownloadRepo.InsertAsync(item);
                                            //}
                                            //if (testType.Equals("TCP Upload"))
                                            //{
                                            //    var item = new Router5GUpload()
                                            //    {
                                            //        source = new DeviceSource
                                            //        {
                                            //            id = deviceId,
                                            //        },
                                            //        time = DateTime.Parse(fetchresult.results.First().updated_at).ToUniversalTime(),
                                            //        type = "5G Device Speed Upload",
                                            //        netdeviceid = fetchresult.results.First().net_device_id.ToString(),
                                            //        Net_5GDeviceUpload = new DeviceRouterUpload
                                            //        {
                                            //            duration = new Device5GData { value = double.Parse(fetchresult.results.First().results.duration) },
                                            //            throughput = new Device5GData { value = double.Parse(fetchresult.results.First().results.throughput) },
                                            //            server_received = new Device5GData { value = double.Parse(fetchresult.results.First().results.server_sent) }
                                            //        }
                                            //    };
                                            //    await measurementUploadRepo.InsertAsync(item);
                                            //}
                                        }

                                        retry = false;
                                    }
                                    else
                                    {
                                        retryCount++;
                                    }
                                }
                                else
                                {

                                }
                            }
                            catch (Exception ex)
                            {
                                var fetchresulterror = client.Get<CradleRouterTestResultErrorDto>(queryCradResult);
                                if (fetchresulterror != null && fetchresulterror.results != null && fetchresulterror.results.First().state.Equals("error"))
                                {
                                    retry = false;
                                }
                            }


                        } while (retry && retryCount < maxRetry);
                    }
                }
            }
            catch (Exception e)
            {
                return false;
            }

            return true;

        }
        private async Task<bool> DownloadTask(string accountid, string testType, string host, string deviceId, string internalid, RestClient client,
            IRepository<Router5GUpload> measurementUploadRepo, IRepository<Router5GDownload> measurementDownloadRepo, IRepository<Router5GLatency> measurementLatencyRepo)
        {
            //foreach (var testType in lstTestType)
            // {
            var retry = true;
            var maxRetry = 10;
            //var maxRetry = 5;
            var retryCount = 0;
            var content = new StringContent(JsonConvert.SerializeObject(new PostModel
            {
                account = "https://www.cradlepointecm.com/api/v2/accounts/" + accountid + "/",
                config = new PostModelConfig
                {
                    host = host,
                    max_test_concurrency = 5,
                    net_device_ids = new int[] { int.Parse(internalid) },
                    port = 12865,
                    size = null,
                    test_timeout = 20,
                    test_type = testType,
                    time = 20
                }
            }), Encoding.UTF8, "application/json");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            content.Headers.Add("X-ECM-API-ID", _restOptions.EcmApiId);
            content.Headers.Add("X-ECM-API-KEY", _restOptions.EcmApiKey);
            content.Headers.Add("X-CP-API-ID", _restOptions.CpApiId);
            content.Headers.Add("X-CP-API-KEY", _restOptions.CpApiKey);
            HttpResponseMessage response;

            try
            {
                response = await _client.PostAsync(_restOptions.Endpoint + "/speed_test/", content);

                if (response.StatusCode != HttpStatusCode.Created)
                {
                    _logger.Warn("Send message don't success: " + response.ReasonPhrase);
                    throw new SmsInvalidException("Send message don't success: " + response.ReasonPhrase);
                }
                else
                {
                    var result = JsonConvert.DeserializeObject<CradleRouterTestDto>(response.Content.ReadAsStringAsync().Result);
                    if (result != null && !string.IsNullOrEmpty(result.id))
                    {
                        //fetch test
                        do
                        {
                            Console.Write("DownloadTask:" + retryCount);
                            //System.Threading.Thread.Sleep(10000);
                            // Fixed 16891
                            System.Threading.Thread.Sleep(3000);
                            var queryCradResult = new RestRequest("speed_test/" + result.id);
                            queryCradResult.AddHeader("X-ECM-API-ID", _restOptions.EcmApiId)
                            .AddHeader("X-ECM-API-KEY", _restOptions.EcmApiKey)
                            .AddHeader("X-CP-API-ID", _restOptions.CpApiId)
                            .AddHeader("X-CP-API-KEY", _restOptions.CpApiKey);
                            try
                            {
                                var fetchresult = client.Get<CradleRouterTestResultDto>(queryCradResult);
                                //insert to DB
                                if (fetchresult != null)
                                {
                                    if (fetchresult.results != null && fetchresult.results.Any())
                                    {
                                        if (!fetchresult.results.First().state.Equals("error"))
                                        {
                                            //if (testType.Equals("TCP Latency"))
                                            //{
                                            //    var item = new Router5GLatency()
                                            //    {
                                            //        source = new DeviceSource
                                            //        {
                                            //            id = deviceId,
                                            //        },
                                            //        time = DateTime.Parse(fetchresult.results.First().updated_at).ToUniversalTime(),
                                            //        type = "5G Device Speed Latency",
                                            //        netdeviceid = fetchresult.results.First().net_device_id.ToString(),
                                            //        Net_5GDeviceLatency = new DeviceRouterLatency
                                            //        {
                                            //            duration = new Device5GData { value = double.Parse(fetchresult.results.First().results.duration) },
                                            //            jitter = new Device5GData { value = double.Parse(fetchresult.results.First().results.jitter) },
                                            //            latency = new Device5GData { value = double.Parse(fetchresult.results.First().results.latency) }
                                            //        }
                                            //    };
                                            //    await measurementLatencyRepo.InsertAsync(item);
                                            //}
                                            //  if (testType.Equals("TCP Download"))
                                            // {
                                            var item = new Router5GDownload()
                                            {
                                                source = new DeviceSource
                                                {
                                                    id = deviceId,
                                                },
                                                time = DateTime.Parse(fetchresult.results.First().updated_at).ToUniversalTime(),
                                                type = "5G Device Speed Download",
                                                netdeviceid = fetchresult.results.First().net_device_id.ToString(),
                                                Net_5GDeviceDownload = new DeviceRouterDownload
                                                {
                                                    duration = new Device5GData { value = double.Parse(fetchresult.results.First().results.duration) },
                                                    throughput = new Device5GData { value = double.Parse(fetchresult.results.First().results.throughput) },
                                                    server_sent = new Device5GData { value = double.Parse(fetchresult.results.First().results.server_sent) }
                                                }
                                            };
                                            await measurementDownloadRepo.InsertAsync(item);
                                            // }
                                            //if (testType.Equals("TCP Upload"))
                                            //{
                                            //    var item = new Router5GUpload()
                                            //    {
                                            //        source = new DeviceSource
                                            //        {
                                            //            id = deviceId,
                                            //        },
                                            //        time = DateTime.Parse(fetchresult.results.First().updated_at).ToUniversalTime(),
                                            //        type = "5G Device Speed Upload",
                                            //        netdeviceid = fetchresult.results.First().net_device_id.ToString(),
                                            //        Net_5GDeviceUpload = new DeviceRouterUpload
                                            //        {
                                            //            duration = new Device5GData { value = double.Parse(fetchresult.results.First().results.duration) },
                                            //            throughput = new Device5GData { value = double.Parse(fetchresult.results.First().results.throughput) },
                                            //            server_received = new Device5GData { value = double.Parse(fetchresult.results.First().results.server_sent) }
                                            //        }
                                            //    };
                                            //    await measurementUploadRepo.InsertAsync(item);
                                            //}
                                        }

                                        retry = false;
                                    }
                                    else
                                    {
                                        retryCount++;
                                    }
                                }
                                else
                                {

                                }
                            }
                            catch (Exception ex)
                            {
                                var fetchresulterror = client.Get<CradleRouterTestResultErrorDto>(queryCradResult);
                                if (fetchresulterror != null && fetchresulterror.results != null && fetchresulterror.results.First().state.Equals("error"))
                                {
                                    retry = false;
                                }
                            }


                        } while (retry && retryCount < maxRetry);
                    }
                }
            }
            catch (Exception e)
            {
                return false;
            }

            return true;

        }
        private async Task<bool> UploadTask(string accountid, string testType, string host, string deviceId, string internalid, RestClient client,
            IRepository<Router5GUpload> measurementUploadRepo, IRepository<Router5GDownload> measurementDownloadRepo, IRepository<Router5GLatency> measurementLatencyRepo)
        {
            //foreach (var testType in lstTestType)
            // {
            var retry = true;
            var maxRetry = 10;
            //var maxRetry = 5;
            var retryCount = 0;
            var content = new StringContent(JsonConvert.SerializeObject(new PostModel
            {
                account = "https://www.cradlepointecm.com/api/v2/accounts/" + accountid + "/",
                config = new PostModelConfig
                {
                    host = host,
                    max_test_concurrency = 5,
                    net_device_ids = new int[] { int.Parse(internalid) },
                    port = 12865,
                    size = null,
                    test_timeout = 20,
                    test_type = testType,
                    time = 20
                }
            }), Encoding.UTF8, "application/json");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            content.Headers.Add("X-ECM-API-ID", _restOptions.EcmApiId);
            content.Headers.Add("X-ECM-API-KEY", _restOptions.EcmApiKey);
            content.Headers.Add("X-CP-API-ID", _restOptions.CpApiId);
            content.Headers.Add("X-CP-API-KEY", _restOptions.CpApiKey);
            HttpResponseMessage response;

            try
            {
                response = await _client.PostAsync(_restOptions.Endpoint + "/speed_test/", content);

                if (response.StatusCode != HttpStatusCode.Created)
                {
                    _logger.Warn("Send message don't success: " + response.ReasonPhrase);
                    throw new SmsInvalidException("Send message don't success: " + response.ReasonPhrase);
                }
                else
                {
                    var result = JsonConvert.DeserializeObject<CradleRouterTestDto>(response.Content.ReadAsStringAsync().Result);
                    if (result != null && !string.IsNullOrEmpty(result.id))
                    {
                        //fetch test
                        do
                        {
                            Console.Write("UploadTask:" + retryCount);
                            //System.Threading.Thread.Sleep(10000);
                            // Fixed 16891
                            System.Threading.Thread.Sleep(3000);
                            var queryCradResult = new RestRequest("speed_test/" + result.id);
                            queryCradResult.AddHeader("X-ECM-API-ID", _restOptions.EcmApiId)
                            .AddHeader("X-ECM-API-KEY", _restOptions.EcmApiKey)
                            .AddHeader("X-CP-API-ID", _restOptions.CpApiId)
                            .AddHeader("X-CP-API-KEY", _restOptions.CpApiKey);
                            try
                            {
                                var fetchresult = client.Get<CradleRouterTestResultDto>(queryCradResult);
                                //insert to DB
                                if (fetchresult != null)
                                {
                                    if (fetchresult.results != null && fetchresult.results.Any())
                                    {
                                        if (!fetchresult.results.First().state.Equals("error"))
                                        {
                                            //if (testType.Equals("TCP Latency"))
                                            //{
                                            //    var item = new Router5GLatency()
                                            //    {
                                            //        source = new DeviceSource
                                            //        {
                                            //            id = deviceId,
                                            //        },
                                            //        time = DateTime.Parse(fetchresult.results.First().updated_at).ToUniversalTime(),
                                            //        type = "5G Device Speed Latency",
                                            //        netdeviceid = fetchresult.results.First().net_device_id.ToString(),
                                            //        Net_5GDeviceLatency = new DeviceRouterLatency
                                            //        {
                                            //            duration = new Device5GData { value = double.Parse(fetchresult.results.First().results.duration) },
                                            //            jitter = new Device5GData { value = double.Parse(fetchresult.results.First().results.jitter) },
                                            //            latency = new Device5GData { value = double.Parse(fetchresult.results.First().results.latency) }
                                            //        }
                                            //    };
                                            //    await measurementLatencyRepo.InsertAsync(item);
                                            //}
                                            //if (testType.Equals("TCP Download"))
                                            //{
                                            //    var item = new Router5GDownload()
                                            //    {
                                            //        source = new DeviceSource
                                            //        {
                                            //            id = deviceId,
                                            //        },
                                            //        time = DateTime.Parse(fetchresult.results.First().updated_at).ToUniversalTime(),
                                            //        type = "5G Device Speed Download",
                                            //        netdeviceid = fetchresult.results.First().net_device_id.ToString(),
                                            //        Net_5GDeviceDownload = new DeviceRouterDownload
                                            //        {
                                            //            duration = new Device5GData { value = double.Parse(fetchresult.results.First().results.duration) },
                                            //            throughput = new Device5GData { value = double.Parse(fetchresult.results.First().results.throughput) },
                                            //            server_sent = new Device5GData { value = double.Parse(fetchresult.results.First().results.server_sent) }
                                            //        }
                                            //    };
                                            //    await measurementDownloadRepo.InsertAsync(item);
                                            //}
                                            // if (testType.Equals("TCP Upload"))
                                            // {
                                            var item = new Router5GUpload()
                                            {
                                                source = new DeviceSource
                                                {
                                                    id = deviceId,
                                                },
                                                time = DateTime.Parse(fetchresult.results.First().updated_at).ToUniversalTime(),
                                                type = "5G Device Speed Upload",
                                                netdeviceid = fetchresult.results.First().net_device_id.ToString(),
                                                Net_5GDeviceUpload = new DeviceRouterUpload
                                                {
                                                    duration = new Device5GData { value = double.Parse(fetchresult.results.First().results.duration) },
                                                    throughput = new Device5GData { value = double.Parse(fetchresult.results.First().results.throughput) },
                                                    server_received = new Device5GData { value = double.Parse(fetchresult.results.First().results.server_sent) }
                                                }
                                            };
                                            await measurementUploadRepo.InsertAsync(item);
                                            // }
                                        }

                                        retry = false;
                                    }
                                    else
                                    {
                                        retryCount++;
                                    }
                                }
                                else
                                {

                                }
                            }
                            catch (Exception ex)
                            {
                                var fetchresulterror = client.Get<CradleRouterTestResultErrorDto>(queryCradResult);
                                if (fetchresulterror != null && fetchresulterror.results != null && fetchresulterror.results.First().state.Equals("error"))
                                {
                                    retry = false;
                                }
                            }


                        } while (retry && retryCount < maxRetry);
                    }
                }
            }
            catch (Exception e)
            {
                return false;
            }

            return true;

        }

        //private async Task<bool> GetDataRouterSpeedTestTask(string devices, string? accountid, string? host, string? testtype, 
        //    IRepository<Router5GUpload> measurementUploadRepo, IRepository<Router5GDownload> measurementDownloadRepo, IRepository<Router5GLatency> measurementLatencyRepo,
        //    HttpClient _client1 )
        //{
        //    if (string.IsNullOrEmpty(accountid))
        //    {
        //        accountid = "101274";
        //    }
        //    if (string.IsNullOrEmpty(host))
        //    {
        //        host = "20.24.141.255";
        //    }
        //    var lstTestType = new List<string>();
        //    if (!string.IsNullOrEmpty(testtype))
        //    {
        //        lstTestType.AddRange(testtype.Split(",").ToList());
        //    }
        //    else
        //    {
        //        lstTestType = new List<string>
        //        { "TCP Latency", "TCP Download", "TCP Upload" };
        //    }
        //    //var measurementUploadRepo = _repositoryFactory.GetRepository<Router5GUpload>("Measurement");
        //    //var measurementDownloadRepo = _repositoryFactory.GetRepository<Router5GDownload>("Measurement");
        //    //var measurementLatencyRepo = _repositoryFactory.GetRepository<Router5GLatency>("Measurement");
        //    var client = new RestClient(_restOptions.Endpoint);

        //    var lstDevice = devices.Split(",");
        //    foreach (var deviceId in lstDevice)
        //    {
        //        var device = _deviceRepository.GetFirstOrDefault(x => x.SourceId.Equals(deviceId));
        //        if (device.Ext_Info != null && device.Ext_Info.InterfaceId != null && device.Ext_Info.InterfaceId.Any())
        //        {
        //            //create test
        //            var lstRequest = new List<CradleRouterTestDto>();
        //            foreach (var testType in lstTestType)
        //            {
        //                var retry = true;
        //                var maxRetry = 10;
        //                var retryCount = 0;
        //                var content = new StringContent(JsonConvert.SerializeObject(new PostModel
        //                {
        //                    account = "https://www.cradlepointecm.com/api/v2/accounts/" + accountid + "/",
        //                    config = new PostModelConfig
        //                    {
        //                        host = host,
        //                        max_test_concurrency = 5,
        //                        net_device_ids = new int[] { int.Parse(device.Ext_Info.InterfaceId.First()) },
        //                        port = 12865,
        //                        size = null,
        //                        test_timeout = 20,
        //                        test_type = testType,
        //                        time = 20
        //                    }
        //                }), Encoding.UTF8, "application/json");
        //                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        //                content.Headers.Add("X-ECM-API-ID", _restOptions.EcmApiId);
        //                content.Headers.Add("X-ECM-API-KEY", _restOptions.EcmApiKey);
        //                content.Headers.Add("X-CP-API-ID", _restOptions.CpApiId);
        //                content.Headers.Add("X-CP-API-KEY", _restOptions.CpApiKey);
        //                HttpResponseMessage response;
        //                try
        //                {
        //                    response = await _client1.PostAsync(_restOptions.Endpoint + "/speed_test/", content);

        //                    if (response.StatusCode != HttpStatusCode.Created)
        //                    {
        //                        _logger.Warn("Send message don't success: " + response.ReasonPhrase);
        //                        throw new SmsInvalidException("Send message don't success: " + response.ReasonPhrase);
        //                    }
        //                    else
        //                    {
        //                        var result = JsonConvert.DeserializeObject<CradleRouterTestDto>(response.Content.ReadAsStringAsync().Result);
        //                        if (result != null && !string.IsNullOrEmpty(result.id))
        //                        {
        //                            //fetch test
        //                            do
        //                            {
        //                                //System.Threading.Thread.Sleep(10000);
        //                                // Fixed 16891
        //                                System.Threading.Thread.Sleep(3000);
        //                                var queryCradResult = new RestRequest("speed_test/" + result.id);
        //                                queryCradResult.AddHeader("X-ECM-API-ID", _restOptions.EcmApiId)
        //                                .AddHeader("X-ECM-API-KEY", _restOptions.EcmApiKey)
        //                                .AddHeader("X-CP-API-ID", _restOptions.CpApiId)
        //                                .AddHeader("X-CP-API-KEY", _restOptions.CpApiKey);
        //                                try
        //                                {
        //                                    var fetchresult = client.Get<CradleRouterTestResultDto>(queryCradResult);
        //                                    //insert to DB
        //                                    if (fetchresult != null)
        //                                    {
        //                                        if (fetchresult.results != null && fetchresult.results.Any())
        //                                        {
        //                                            if (!fetchresult.results.First().state.Equals("error"))
        //                                            {
        //                                                if (testType.Equals("TCP Latency"))
        //                                                {
        //                                                    var item = new Router5GLatency()
        //                                                    {
        //                                                        source = new DeviceSource
        //                                                        {
        //                                                            id = deviceId,
        //                                                        },
        //                                                        time = DateTime.Parse(fetchresult.results.First().updated_at).ToUniversalTime(),
        //                                                        type = "5G Device Speed Latency",
        //                                                        netdeviceid = fetchresult.results.First().net_device_id.ToString(),
        //                                                        Net_5GDeviceLatency = new DeviceRouterLatency
        //                                                        {
        //                                                            duration = new Device5GData { value = double.Parse(fetchresult.results.First().results.duration) },
        //                                                            jitter = new Device5GData { value = double.Parse(fetchresult.results.First().results.jitter) },
        //                                                            latency = new Device5GData { value = double.Parse(fetchresult.results.First().results.latency) }
        //                                                        }
        //                                                    };
        //                                                    await measurementLatencyRepo.InsertAsync(item);
        //                                                }
        //                                                if (testType.Equals("TCP Download"))
        //                                                {
        //                                                    var item = new Router5GDownload()
        //                                                    {
        //                                                        source = new DeviceSource
        //                                                        {
        //                                                            id = deviceId,
        //                                                        },
        //                                                        time = DateTime.Parse(fetchresult.results.First().updated_at).ToUniversalTime(),
        //                                                        type = "5G Device Speed Download",
        //                                                        netdeviceid = fetchresult.results.First().net_device_id.ToString(),
        //                                                        Net_5GDeviceDownload = new DeviceRouterDownload
        //                                                        {
        //                                                            duration = new Device5GData { value = double.Parse(fetchresult.results.First().results.duration) },
        //                                                            throughput = new Device5GData { value = double.Parse(fetchresult.results.First().results.throughput) },
        //                                                            server_sent = new Device5GData { value = double.Parse(fetchresult.results.First().results.server_sent) }
        //                                                        }
        //                                                    };
        //                                                    await measurementDownloadRepo.InsertAsync(item);
        //                                                }
        //                                                if (testType.Equals("TCP Upload"))
        //                                                {
        //                                                    var item = new Router5GUpload()
        //                                                    {
        //                                                        source = new DeviceSource
        //                                                        {
        //                                                            id = deviceId,
        //                                                        },
        //                                                        time = DateTime.Parse(fetchresult.results.First().updated_at).ToUniversalTime(),
        //                                                        type = "5G Device Speed Upload",
        //                                                        netdeviceid = fetchresult.results.First().net_device_id.ToString(),
        //                                                        Net_5GDeviceUpload = new DeviceRouterUpload
        //                                                        {
        //                                                            duration = new Device5GData { value = double.Parse(fetchresult.results.First().results.duration) },
        //                                                            throughput = new Device5GData { value = double.Parse(fetchresult.results.First().results.throughput) },
        //                                                            server_received = new Device5GData { value = double.Parse(fetchresult.results.First().results.server_sent) }
        //                                                        }
        //                                                    };
        //                                                    await measurementUploadRepo.InsertAsync(item);
        //                                                }
        //                                            }

        //                                            retry = false;
        //                                        }
        //                                        else
        //                                        {
        //                                            retryCount++;
        //                                        }
        //                                    }
        //                                    else
        //                                    {

        //                                    }
        //                                }
        //                                catch (Exception ex)
        //                                {
        //                                    var fetchresulterror = client.Get<CradleRouterTestResultErrorDto>(queryCradResult);
        //                                    if (fetchresulterror != null && fetchresulterror.results != null && fetchresulterror.results.First().state.Equals("error"))
        //                                    {
        //                                        retry = false;
        //                                    }
        //                                }


        //                            } while (retry && retryCount < maxRetry);
        //                        }
        //                    }
        //                }
        //                catch (Exception e)
        //                {
        //                    return false;
        //                }



        //            }




        //        }
        //    }
        //    return true;

        //}
        public void CheckDeviceAndReturnDefault(string id)
        {
            var device = _repositoryFactory.GetRepository<WE.IOTService.Domain.Entities.Device>("Device").GetFirstOrDefault(x => x.SourceId == id);
        }

        public async Task<PowerSunlightGeneratedHourlyDto> GetPowerGeneratedHourly(string id, string? irRaddianceId)
        {

            CheckDeviceAndReturnDefault(id);

            var StartDate = DateTime.Now.AddDays(-1);
            var StartLastHourDate = DateTime.Now.AddMinutes(-DateTime.Now.Minute).AddSeconds(-DateTime.Now.Second - 1);
            var EndDateLastHour = DateTime.Now;
            //#12595 update hourly chart for power return all data without aggregration
            //PipelineDefinition<dynamic, PowerHourly> pipeline = new BsonDocument[]
            //{
            //    new BsonDocument("$match",
            //    new BsonDocument
            //        {
            //            { "time",
            //    new BsonDocument
            //            {
            //                { "$gte",
            //    StartDate },
            //                { "$lte",
            //    StartLastHourDate }
            //            } },
            //        { "source.id",
            //    new BsonDocument("$eq", id) },
            //            { "type",
            //    new BsonDocument("$in",
            //    new BsonArray
            //                {
            //                    "mcx_PV"
            //                }) }
            //        }),
            //    new BsonDocument("$sort",
            //    new BsonDocument("time", 1)),
            //    new BsonDocument("$group",
            //    new BsonDocument
            //        {
            //            { "_id",
            //    new BsonDocument("$hour", "$time") },
            //            { "TotalPower",
            //    new BsonDocument("$sum",
            //    new BsonDocument("$divide",
            //    new BsonArray
            //                    {
            //                        new BsonDocument("$multiply",
            //                        new BsonArray
            //                            {
            //                                "$mcx_PV.voltage.value",
            //                                "$mcx_PV.current.value"
            //                            }),
            //                        convert to kW
            //                        6*1000
            //                    })) }
            //        })
            //};
            //PipelineDefinition<dynamic, Irradiance> pipelineIrr = new BsonDocument[]
            //{
            //    new BsonDocument("$match",
            //    new BsonDocument
            //        {
            //            { "sources", string.IsNullOrEmpty(irRaddianceId) ? "" : irRaddianceId },
            //            { "period_end",
            //    new BsonDocument
            //            {
            //                { "$gte", StartDate.ToString("yyyy-MM-ddTHH:mm:ssZ") },
            //                { "$lte", StartLastHourDate.ToString("yyyy-MM-ddTHH:mm:ssZ") }
            //            } }
            //        })
            //};

            PipelineDefinition<BsonDocument, LastHourlyDto> pipelineLastHour = new BsonDocument[]
            {
                new BsonDocument("$match",
                new BsonDocument
                    {
                        { "time",
                new BsonDocument
                        {
                            { "$gte",
                StartDate },
                            { "$lte",
                EndDateLastHour }
                        } },
                    { "source.id",
                new BsonDocument("$eq", id) },
                        { "type",
                new BsonDocument("$in",
                new BsonArray
                            {
                                "mcx_PV"
                            }) }
                    }),
                new BsonDocument("$sort",
                new BsonDocument("time", 1)),
                new BsonDocument("$project",
                new BsonDocument
                    {
                        { "_id", "$time" },
                        { "Value",
                new BsonDocument("$divide",
                new BsonArray
                            {
                                new BsonDocument("$multiply",
                                new BsonArray
                                    {
                                        "$mcx_PV.voltage.value",
                                        "$mcx_PV.current.value"
                                    }),
                                6
                            }) }
                    })
            };
            PipelineDefinition<BsonDocument, Irradiance> pipelineIrrLastHour = new BsonDocument[]
            {
                new BsonDocument("$match",
                new BsonDocument
                    {
                        { "sources", string.IsNullOrEmpty(irRaddianceId) ? "" : irRaddianceId },
                        { "period_end",
                new BsonDocument
                        {
                            { "$gte", StartDate.ToString("yyyy-MM-ddTHH:mm:ssZ") },
                            { "$lte", EndDateLastHour.ToString("yyyy-MM-ddTHH:mm:ssZ") }
                        } }
                    })
            };
            //query data
            //var db = _mongoClient.GetDatabase(_tenantService.Database);
            //var meaCollection = db.GetCollection<dynamic>("Measurement");
            //var irrCollection = db.GetCollection<dynamic>("Irradiance");
            var measurementRespo = _repositoryFactory.GetRepository<BsonDocument>("Measurement");
            var irrRespo = _repositoryFactory.GetRepository<BsonDocument>("Irradiance");

            //var datapower = await meaCollection.AggregateAsync<PowerHourly>(pipeline);
            //var dataIrr = await irrCollection.AggregateAsync<Irradiance>(pipelineIrr);

            var lasthourdata = await measurementRespo.Collection.AggregateAsync<LastHourlyDto>(pipelineLastHour);
            var lasthourirr = await irrRespo.Collection.AggregateAsync<Irradiance>(pipelineIrrLastHour);

            //return result
            //var irrDatairr = dataIrr.ToList().OrderBy(x => DateTime.Parse(x.period_end)).GroupBy(x => DateTime.Parse(x.period_end).Hour).Select(g => new HourlyDto { _id = g.Key.ToString(), Value = g.Average(x => x.dni) });
            //var lstDataPower = datapower.ToList().Select(x => new HourlyDto { _id = x._id.ToString(), Value = x.TotalPower });
            var lstDataPower = lasthourdata.ToList().Select(x => new HourlyDto { _id = x._id, Value = x.Value });
            var lstDataIrr = lasthourirr.ToList().OrderBy(x => DateTime.Parse(x.period_end)).Select(x => new HourlyDto { _id = DateTime.Parse(x.period_end), Value = x.dni });

            return new PowerSunlightGeneratedHourlyDto() { TotalPower = lstDataPower.ToList(), SunLight = lstDataIrr.ToList() };
        }

        public async Task<List<SingleChartQueryResult>> GetFiveGSignalHourly(string id)
        {
            CheckDeviceAndReturnDefault(id);

            var StartDate = DateTime.Now.AddDays(-1);
            var EndDate = DateTime.Now;
            PipelineDefinition<BsonDocument, SingleChartQueryResult> pipeline = new BsonDocument[]
            {
                new BsonDocument("$match",
                new BsonDocument
                    {
                        { "time",
                new BsonDocument
                        {
                            { "$gte",
                StartDate },
                            { "$lte",
                EndDate }
                        } },
                        { "source.id",
                new BsonDocument("$eq", id) },
                        { "type",
                new BsonDocument("$in",
                new BsonArray
                            {
                                "5G Device Signal"
                            }) }
                    }),
                new BsonDocument("$sort",
                new BsonDocument("time", 1)),
                new BsonDocument("$group",
                new BsonDocument
                    {
                        { "_id",
                new BsonDocument("$hour", "$time") },
                        { "Value",
                new BsonDocument("$avg", "$Net_5GSignal.rsrp5g.value") }
                    })
            };
            //var db = _mongoClient.GetDatabase(_tenantService.Database);
            //var meaCollection = db.GetCollection<dynamic>("Measurement");
            var measurementRespo = _repositoryFactory.GetRepository<BsonDocument>("Measurement");
            var data = await measurementRespo.Collection.AggregateAsync<SingleChartQueryResult>(pipeline);

            return data.ToList();
        }

        public async Task<SingleValueDto> GetGenericSingleValue(GenericParamModel model)
        {
            //var StartDate = DateTime.UtcNow.AddDays(-1);
            var StartDate = DateTime.UtcNow.AddMinutes(-9);    //fetch data starting from less than 10minutes ago
            var EndDate = DateTime.UtcNow;
            var formatedDatapath = string.Empty;
            var datapath = model.DataPath.Split(",");
            foreach (var s in datapath)
            {
                if (s != datapath[0])
                {
                    formatedDatapath = formatedDatapath + s.ToLower();
                }
                else
                {
                    formatedDatapath = formatedDatapath + s;
                }
            }


            switch (model.Periodicity)
            {
                case DataConstants.Periodicity.daily:
                    StartDate = DateTime.UtcNow.AddDays(-1);
                    break;
                case DataConstants.Periodicity.weekly:
                    StartDate = DateTime.UtcNow.StartOfWeek(DayOfWeek.Monday);

                    break;
                case DataConstants.Periodicity.monthly:
                    StartDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

                    break;
                case DataConstants.Periodicity.yearly:
                    StartDate = new DateTime(DateTime.UtcNow.Year, 1, 1);

                    break;
            }

            PipelineDefinition<BsonDocument, BsonDocument> pipeline = new BsonDocument[]
            {
                //generic filtering
                new BsonDocument("$match",
                new BsonDocument
                    {
                        { "time",
                new BsonDocument
                        {
                            { "$gte",
                StartDate },
                            { "$lte",
                EndDate }
                        } },
                        { "source.id", string.IsNullOrEmpty(model.SourceId) ? new BsonDocument("$ne", BsonNull.Value) : model.SourceId },
                        { "$or",
                                        new BsonArray
                                                {
                                                    new BsonDocument("source.subId",
                                                    new BsonDocument("$exists", false)),
                                                    new BsonDocument("source.subId", string.IsNullOrEmpty(model.SubId) ? new BsonDocument("$ne", BsonNull.Value) : model.SubId)
                                                } },
                        {"type", model.DataPoint }
                    }),
            };
            if (model.DataAggregateType.HasValue)
            {
                switch (model.DataAggregateType.Value)
                {
                    case DataConstants.DataAggregateType.Current:
                        PipelineStageDefinition<BsonDocument, BsonDocument> stagesort = new BsonDocument {
                            { "$sort",
                                new BsonDocument("time", -1)
                            }
                        };
                        PipelineStageDefinition<BsonDocument, BsonDocument> stagelimit = new BsonDocument {
                            { "$limit",
                                1
                            }
                        };
                        PipelineStageDefinition<BsonDocument, BsonDocument> stageproject =
                                new BsonDocument("$project",
                                new BsonDocument
                                {
                                    { "_id",
                                        "$source.id"
                                    },
                                    { "Value","$"+formatedDatapath
                                    },
                                    {
                                        "Time",
                                        "$time"
                                    }
                                });
                        pipeline = pipeline.AppendStage<BsonDocument, BsonDocument, BsonDocument>(stagesort)
                            .AppendStage<BsonDocument, BsonDocument, BsonDocument>(stagelimit)
                            .AppendStage<BsonDocument, BsonDocument, BsonDocument>(stageproject);
                        break;
                    case DataConstants.DataAggregateType.Sum:
                        PipelineStageDefinition<BsonDocument, BsonDocument> stagesum = new BsonDocument {
                            { "$group",
                                new BsonDocument
                                {
                                    { "_id",
                                        "$source.id"
                                    },
                                    { "Value",
                                        new BsonDocument("$sum", "$"+formatedDatapath)
                                    },
                                    {
                                        "Time",
                                        new BsonDocument("$max","$time")
                                    }
                                }
                            }
                        };
                        pipeline = pipeline.AppendStage<BsonDocument, BsonDocument, BsonDocument>(stagesum);
                        break;
                    default:
                        PipelineStageDefinition<BsonDocument, BsonDocument> stageavg = new BsonDocument {
                            { "$group",
                                new BsonDocument
                                {
                                    { "_id",
                                        "$source.id"
                                    },
                                    { "Value",
                                        new BsonDocument("$avg", "$"+formatedDatapath)
                                    },
                                    {
                                        "Time",
                                        new BsonDocument("$max","$time")
                                    }
                                }
                            }
                        };
                        pipeline = pipeline.AppendStage<BsonDocument, BsonDocument, BsonDocument>(stageavg);
                        break;
                }
            }

            var measurementRespo = _repositoryFactory.GetRepository<BsonDocument>("Measurement");
            var data = await measurementRespo.Collection.AggregateAsync<BsonDocument>(pipeline);
            var items = data.ToList();
            if (items.Any())
            {
                var result = BsonSerializer.Deserialize<SingleValueDto>(items.FirstOrDefault());
                if (result.Value != null)
                {
                    if (!string.IsNullOrEmpty(model.Formular) && model.Formular.IndexOf("[x]") > -1)
                    {
                        result.Value = EvaluateFormula(model.Formular.Replace("[x]", result.Value.ToString()));
                    }
                    result.Value = Math.Round((double)result.Value, 2);
                }
                return result;
            }
            return new SingleValueDto();
        }

        public async Task<List<SingleChartDto>> GetGenericSingleChartValue(GenericParamModel model)
        {
            var StartDate = DateTime.UtcNow.AddDays(-1);
            var EndDate = DateTime.UtcNow.AddHours(1);
            var formatedDatapath = string.Empty;
            var datapath = model.DataPath.Split(",");
            var aggTime = "$hour";
            var defaultResult = new List<SingleChartQueryResult>();
            foreach (var s in datapath)
            {
                if (s != datapath[0])
                {
                    formatedDatapath = formatedDatapath + s.ToLower();
                }
                else
                {
                    formatedDatapath = formatedDatapath + s;
                }
            }


            switch (model.Periodicity)
            {
                case DataConstants.Periodicity.daily:
                    DateTime.UtcNow.AddHours(-DateTime.UtcNow.Hour).AddMinutes(-DateTime.UtcNow.Minute);
                    break;
                case DataConstants.Periodicity.weekly:
                    StartDate = DateTime.Now.StartOfWeek(DayOfWeek.Monday);

                    break;
                case DataConstants.Periodicity.monthly:
                    StartDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                    break;
                case DataConstants.Periodicity.yearly:
                    StartDate = new DateTime(DateTime.Now.Year, 1, 1);

                    break;
            }
            switch (model.DataResultType)
            {
                case DataConstants.DataResultType.Hourly:
                    aggTime = "$hour";
                    break;
                case DataConstants.DataResultType.Daily:
                    aggTime = "$dayOfMonth";

                    break;
                case DataConstants.DataResultType.Weekly:
                    aggTime = "$week";

                    break;
            }
            PipelineDefinition<BsonDocument, BsonDocument> pipeline = new BsonDocument[]
            {
                //generic filtering
                new BsonDocument("$match",
                new BsonDocument
                    {
                        { "time",
                new BsonDocument
                        {
                            { "$gte",
                StartDate },
                            { "$lte",
                EndDate }
                        } },
                        { "source.id", string.IsNullOrEmpty(model.SourceId) ? new BsonDocument("$ne", BsonNull.Value) : model.SourceId },
                        { "$or",
                                        new BsonArray
                                                {
                                                    new BsonDocument("source.subId",
                                                    new BsonDocument("$exists", false)),
                                                    new BsonDocument("source.subId", string.IsNullOrEmpty(model.SubId) ? new BsonDocument("$ne", BsonNull.Value) : model.SubId)
                                                } },
                        {"type", model.DataPoint }
                    }),
            };
            PipelineStageDefinition<BsonDocument, BsonDocument> stagesort = new BsonDocument {
                            { "$sort",
                                new BsonDocument("time", 1)
                            }
                        };
            if (model.DataAggregateType.HasValue)
            {
                switch (model.DataAggregateType.Value)
                {
                    case DataConstants.DataAggregateType.Current:
                        PipelineStageDefinition<BsonDocument, BsonDocument> stagerecord = new BsonDocument {
                            { "$project",
                                new BsonDocument
                                {
                                    { "_id","$time"
                                    },
                                    {
                                      "Point", "$time"
                                    },
                                    { "Value","$"+formatedDatapath
                                    }
                                }
                            }
                        };
                        pipeline = pipeline.AppendStage<BsonDocument, BsonDocument, BsonDocument>(stagesort)
                            .AppendStage<BsonDocument, BsonDocument, BsonDocument>(stagerecord);
                        break;
                    case DataConstants.DataAggregateType.Sum:
                        PipelineStageDefinition<BsonDocument, BsonDocument> stagesum = new BsonDocument {
                            { "$group",
                                new BsonDocument
                                {
                                    { "_id",
                                        new BsonDocument(aggTime, "$time")
                                    },
                                    {
                                      "Point", new BsonDocument("$min", "$time")
                                    },
                                    { "Value",
                                        new BsonDocument("$sum", "$"+formatedDatapath)
                                    }
                                }
                            }
                        };
                        pipeline = pipeline.AppendStage<BsonDocument, BsonDocument, BsonDocument>(stagesort)
                            .AppendStage<BsonDocument, BsonDocument, BsonDocument>(stagesum);
                        break;
                    default:
                        PipelineStageDefinition<BsonDocument, BsonDocument> stageavg = new BsonDocument {
                            { "$group",
                                new BsonDocument
                                {
                                    { "_id",
                                        new BsonDocument(aggTime, "$time")
                                    },
                                    {
                                      "Point", new BsonDocument("$min", "$time")
                                    },
                                    { "Value",
                                        new BsonDocument("$avg", "$"+formatedDatapath)
                                    }
                                }
                            }
                        };
                        pipeline = pipeline.AppendStage<BsonDocument, BsonDocument, BsonDocument>(stagesort)
                            .AppendStage<BsonDocument, BsonDocument, BsonDocument>(stageavg);
                        break;
                }
            }

            var measurementRespo = _repositoryFactory.GetRepository<BsonDocument>("Measurement");
            var data = await measurementRespo.Collection.AggregateAsync<BsonDocument>(pipeline);


            if (model.DataAggregateType.Value == DataConstants.DataAggregateType.Current)
            {
                var resultraw = data.ToList().Select(x => BsonSerializer.Deserialize<SingleChartQueryRawResult>(x));
                return resultraw.Select(x => new SingleChartDto { Point = x.Point.Value.AddHours(model.TimeOffset.Value).ToString(), Value = x.Value }).ToList();
            }
            var result = data.ToList().Select(x => BsonSerializer.Deserialize<SingleChartQueryResult>(x));
            //if (defaultResult.Any())
            //{
            //    //update logic 24 hour later
            //    //var fullResult = defaultResult.Select(x => new SingleChartQueryResult { _id = x._id, Value = result.Any(y => y._id == x._id) ? (result.FirstOrDefault(y => y._id == x._id).Value != null ? result.FirstOrDefault(y => y._id == x._id).Value : 0) : x.Value })
            //    //    .Select(x => new SingleChartDto { Point = ConvertTime(model.TimeOffset.Value, x._id.Value).ToString(), Value = x.Value }).ToList();
            //    ////logic to force return data when utc init by server not stable
            //    //do
            //    //{
            //    //    fullResult = fullResult.Take(fullResult.Count - 1).ToList();
            //    //} while (fullResult.Last().Value != 0 && fullResult.Count >= 23);
            //    //return fullResult;
            //}
            return result.Select(x => new SingleChartDto { Point = x.Point.Value.AddHours(model.TimeOffset.Value).ToString(), Value = ((string.IsNullOrEmpty(model.Formular) || model.Formular.IndexOf("[x]") == -1) ? x.Value : EvaluateFormula(model.Formular.Replace("[x]", x.Value.ToString()))) }).ToList();

        }

        public async Task<List<MultiChartDto>> GetGenericMultiChartValue(MultiChartGenericParamModel model)
        {
            var widgetData = _widgetRepository.GetFirstOrDefault(d => d.Id == model.WidgetId);
            var lstResults = new List<MultiChartDto>();

            var additionalInfo = new WidgetAdditionalDataList
            {
                Color = "gray",
                IndicatorName = widgetData?.LineName,
                Unit = widgetData?.Unit
            };
            var baseResult = await GetSingleSourceData(model, additionalInfo);
            lstResults.Add(baseResult);
            var widgetAdditionalData = widgetData?.Setting?.WidgetAdditionalDataList;
            if (widgetAdditionalData != null && widgetAdditionalData.Any())
            {
                foreach (var source in widgetAdditionalData)
                {
                    model.SourceId = source.SourceId;
                    model.SubId = source.SubId;
                    model.DataPath = source.DataPath;
                    model.DataPoint = source.DataPoint;
                    var additionalResult = await GetSingleSourceData(model, source);
                    lstResults.Add(additionalResult);
                }
            }
            return lstResults;
        }

        private async Task<MultiChartDto> GetSingleSourceData(MultiChartGenericParamModel model, WidgetAdditionalDataList additionalInfo)
        {
            var StartDate = DateTime.UtcNow.AddDays(-1);
            var EndDate = DateTime.UtcNow.AddHours(1);
            var formatedDatapath = string.Empty;
            var datapath = model.DataPath.Split(",");
            var aggTime = "$hour";
            var defaultResult = new List<SingleChartQueryResult>();
            foreach (var s in datapath)
            {
                if (s != datapath[0])
                {
                    formatedDatapath = formatedDatapath + s.ToLower();
                }
                else
                {
                    formatedDatapath = formatedDatapath + s;
                }
            }


            switch (model.Periodicity)
            {
                case DataConstants.Periodicity.daily:
                    DateTime.UtcNow.AddHours(-DateTime.UtcNow.Hour).AddMinutes(-DateTime.UtcNow.Minute);
                    break;
                case DataConstants.Periodicity.weekly:
                    StartDate = DateTime.Now.StartOfWeek(DayOfWeek.Monday);

                    break;
                case DataConstants.Periodicity.monthly:
                    StartDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

                    break;
                case DataConstants.Periodicity.yearly:
                    StartDate = new DateTime(DateTime.Now.Year, 1, 1);

                    break;
            }
            switch (model.DataResultType)
            {
                case DataConstants.DataResultType.Hourly:
                    aggTime = "$hour";
                    break;
                case DataConstants.DataResultType.Daily:
                    aggTime = "$dayOfMonth";

                    break;
                case DataConstants.DataResultType.Weekly:
                    aggTime = "$week";

                    break;
            }
            PipelineDefinition<BsonDocument, BsonDocument> pipeline = new BsonDocument[]
            {
                //generic filtering
                new BsonDocument("$match",
                new BsonDocument
                    {
                        { "time",
                new BsonDocument
                        {
                            { "$gte",
                StartDate },
                            { "$lte",
                EndDate }
                        } },
                        { "source.id", string.IsNullOrEmpty(model.SourceId) ? new BsonDocument("$ne", BsonNull.Value) : model.SourceId },
                        { "$or",
                                        new BsonArray
                                                {
                                                    new BsonDocument("source.subId",
                                                    new BsonDocument("$exists", false)),
                                                    new BsonDocument("source.subId", string.IsNullOrEmpty(model.SubId) ? new BsonDocument("$ne", BsonNull.Value) : model.SubId)
                                                } },
                        {"type", model.DataPoint }
                    }),
            };
            PipelineStageDefinition<BsonDocument, BsonDocument> stagesort = new BsonDocument {
                            { "$sort",
                                new BsonDocument("time", 1)
                            }
                        };
            if (model.DataAggregateType.HasValue)
            {
                switch (model.DataAggregateType.Value)
                {
                    case DataConstants.DataAggregateType.Current:
                        PipelineStageDefinition<BsonDocument, BsonDocument> stagerecord = new BsonDocument {
                            { "$project",
                                new BsonDocument
                                {
                                    { "_id","$time"
                                    },
                                    {
                                      "Point", "$time"
                                    },
                                    { "Value","$"+formatedDatapath
                                    }
                                }
                            }
                        };
                        pipeline = pipeline.AppendStage<BsonDocument, BsonDocument, BsonDocument>(stagesort)
                            .AppendStage<BsonDocument, BsonDocument, BsonDocument>(stagerecord);
                        break;
                    case DataConstants.DataAggregateType.Sum:
                        PipelineStageDefinition<BsonDocument, BsonDocument> stagesum = new BsonDocument {
                            { "$group",
                                new BsonDocument
                                {
                                    { "_id",
                                        new BsonDocument(aggTime, "$time")
                                    },
                                    {
                                      "Point", new BsonDocument("$min", "$time")
                                    },
                                    { "Value",
                                        new BsonDocument("$sum", "$"+formatedDatapath)
                                    }
                                }
                            }
                        };
                        pipeline = pipeline.AppendStage<BsonDocument, BsonDocument, BsonDocument>(stagesort)
                            .AppendStage<BsonDocument, BsonDocument, BsonDocument>(stagesum);
                        break;
                    default:
                        PipelineStageDefinition<BsonDocument, BsonDocument> stageavg = new BsonDocument {
                            { "$group",
                                new BsonDocument
                                {
                                    { "_id",
                                        new BsonDocument(aggTime, "$time")
                                    },
                                    {
                                      "Point", new BsonDocument("$min", "$time")
                                    },
                                    { "Value",
                                        new BsonDocument("$avg", "$"+formatedDatapath)
                                    }
                                }
                            }
                        };
                        pipeline = pipeline.AppendStage<BsonDocument, BsonDocument, BsonDocument>(stagesort)
                            .AppendStage<BsonDocument, BsonDocument, BsonDocument>(stageavg);
                        break;
                }
            }

            var measurementRespo = _repositoryFactory.GetRepository<BsonDocument>("Measurement");
            var data = await measurementRespo.Collection.AggregateAsync<BsonDocument>(pipeline);
            if (model.DataAggregateType.Value == DataConstants.DataAggregateType.Current)
            {
                var resultraw = data.ToList().Select(x => BsonSerializer.Deserialize<SingleChartQueryRawResult>(x));
                var itemsraw = resultraw.Select(x => new SingleChartDto { Point = x.Point.Value.AddHours(model.TimeOffset.Value).ToString(), Value = x.Value }).ToList();
                return new MultiChartDto()
                {
                    Color = additionalInfo?.Color,
                    Unit = additionalInfo?.Unit,
                    IndicatorName = additionalInfo?.IndicatorName,
                    Values = itemsraw
                };
            }
            var result = data.ToList().Select(x => BsonSerializer.Deserialize<SingleChartQueryResult>(x));
            var items = result.Select(x => new SingleChartDto { Point = x.Point.Value.AddHours(model.TimeOffset.Value).ToString(), Value = ((string.IsNullOrEmpty(model.Formular) || model.Formular.IndexOf("[x]") == -1) ? x.Value : EvaluateFormula(model.Formular.Replace("[x]", x.Value.ToString()))) }).ToList();

            return new MultiChartDto()
            {
                Color = additionalInfo?.Color,
                Unit = additionalInfo?.Unit,
                IndicatorName = additionalInfo?.IndicatorName,
                Values = items
            };
        }

        private static int ConvertTime(int? timeOffset, int hour)
        {
            var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);

            return (hour + (timeOffset.HasValue ? timeOffset.Value : offset.Hours)) % 24;
        }
        //private List<SingleChartQueryResult> DefaultValueByTimeRange(DateTime startDate, DateTime endDate, DataResultType agg)
        //{
        //    var lstTime = new List<SingleChartQueryResult>();
        //    int i = 0;
        //    switch (agg)
        //    {
        //        case DataConstants.DataResultType.Hourly:
        //            while (startDate.AddHours(i) < endDate)
        //            {
        //                lstTime.Add(new SingleChartQueryResult { Value = 0 , _id = startDate.AddHours(i).Hour }) ;
        //                i++;
        //            }
        //            break;
        //        case DataConstants.DataResultType.Daily:
        //            while (startDate.AddDays(i) < endDate)
        //            {
        //                lstTime.Add(new SingleChartQueryResult { Value = 0, _id = startDate.AddDays(i).Day });
        //                i++;
        //            }
        //            break;
        //        case DataConstants.DataResultType.Weekly:
        //            while (startDate.AddWeeks(i) < endDate)
        //            {
        //                Calendar cal = new CultureInfo("en-US").Calendar;
        //                int week = cal.GetWeekOfYear(startDate.AddWeeks(i), CalendarWeekRule.FirstDay, DayOfWeek.Monday);

        //                lstTime.Add(new SingleChartQueryResult { Value = 0, _id = week });
        //                i++;
        //            }
        //            break;
        //    }
        //    return lstTime.Skip(2).Take(i-1).ToList();
        //}

        //private async Task<SingleValueDto?> GetLastGenericSingleValueData(GenericParamModel model)
        private async Task<SingleIdDto?> GetLastGenericSingleValueData(GenericParamModel model)
        {
            var StartDate = DateTime.UtcNow.AddDays(-30);
            var EndDate = DateTime.UtcNow;
            var formatedDatapath = string.Empty;
            var datapath = model.DataPath.Split(",");
            foreach (var s in datapath)
            {
                if (s != datapath[0])
                {
                    formatedDatapath = formatedDatapath + s.ToLower();
                }
                else
                {
                    formatedDatapath = formatedDatapath + s;
                }
            }

            PipelineDefinition<BsonDocument, BsonDocument> pipeline = new BsonDocument[]
            {
                //generic filtering
                new BsonDocument("$match",
                new BsonDocument
                    {
                        { "time",
                new BsonDocument
                        {
                            { "$gte",
                StartDate },
                            { "$lte",
                EndDate }
                        } },
                        { "source.id", string.IsNullOrEmpty(model.SourceId) ? new BsonDocument("$ne", BsonNull.Value) : model.SourceId },
                        { "$or",
                                        new BsonArray
                                                {
                                                    new BsonDocument("source.subId",
                                                    new BsonDocument("$exists", false)),
                                                    new BsonDocument("source.subId", string.IsNullOrEmpty(model.SubId) ? new BsonDocument("$ne", BsonNull.Value) : model.SubId)
                                                } },
                        {"type", model.DataPoint }
                    }),
            };
            PipelineStageDefinition<BsonDocument, BsonDocument> stagesort = new BsonDocument {
                            { "$sort",
                                new BsonDocument("time", -1)
                            }
                        };
            PipelineStageDefinition<BsonDocument, BsonDocument> stagelimit = new BsonDocument {
                            { "$limit",
                                1
                            }
                        };
            PipelineStageDefinition<BsonDocument, BsonDocument> stageproject =
                    new BsonDocument("$project",
                    new BsonDocument
                    {
                                    { "_id",
                                        "$created_at_timeuuid"
                                    },
                                    //{ "Value","$"+formatedDatapath
                                    //},
                                    {
                                        "Time",
                                        "$time"
                                    }
                    });
            pipeline = pipeline.AppendStage<BsonDocument, BsonDocument, BsonDocument>(stagesort)
                .AppendStage<BsonDocument, BsonDocument, BsonDocument>(stagelimit)
                .AppendStage<BsonDocument, BsonDocument, BsonDocument>(stageproject);

            var measurementRespo = _repositoryFactory.GetRepository<BsonDocument>("Measurement");
            var data = await measurementRespo.Collection.AggregateAsync<BsonDocument>(pipeline);
            var items = data.ToList();
            if (items.Any())
            {
                //var result = BsonSerializer.Deserialize<SingleValueDto>(items.FirstOrDefault());
                var result = BsonSerializer.Deserialize<SingleIdDto>(items.FirstOrDefault());
                return result;
            }
            return null;
        }
        public class PostModel
        {
            public string account { get; set; }
            public PostModelConfig config { get; set; }
        }
        public class PostModelConfig
        {
            public string host { get; set; }
            public int max_test_concurrency { get; set; }
            public int[] net_device_ids { get; set; }
            public int port { get; set; }
            public int? size { get; set; }
            public int? test_timeout { get; set; }
            public string test_type { get; set; }
            public int? time { get; set; }
        }
        private static double EvaluateFormula(string formula)
        {
            // Create a new DataTable object
            DataTable table = new DataTable();
            // Compute the formula and convert the result to double
            double result = Convert.ToDouble(table.Compute(formula, null));
            // Return the result
            return result;
        }
    }
}
