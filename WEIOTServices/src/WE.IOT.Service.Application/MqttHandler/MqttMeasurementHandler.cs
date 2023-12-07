using Common.Mongodb.Repository;
using Common.MongoDb.Audits;
using Common.MongoDb.Repository;
using Common.Mqtt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Graph;
using MongoDB.Bson;
using MongoDB.Driver;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using Newtonsoft.Json.Linq;
using SharpCompress.Common;
using System.Collections;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using WE.IOT.Service.Application.Models.Device;
using Microsoft.AspNetCore.Http;
using Org.BouncyCastle.Utilities.Encoders;
using System.Globalization;
using static System.Net.Mime.MediaTypeNames;
using Microsoft.AspNetCore.SignalR;
using WE.IOT.Service.SignalRHub;
using WE.IotService.Application.Models;
using Common.Threshold;
using Common.MongoDb.Entities;
using Common.Mongodb;
using SendGrid.Helpers.Mail.Model;

namespace WE.IOT.Service.Application.MqttHandler;

public class MqttMeasurementHandler : IHostedService, IDisposable
{
    private MqttMeasurementService _service;
    public MqttMeasurementHandler(MqttMeasurementService service)
    {
        this._service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _service.InitializeAsync();
        await _service.ConnectAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
    public void Dispose()
    {
        _service.Dispose();
    }
}

public class MqttMeasurementService
{
    private IManagedMqttClient _managedMqttClient;
    private ManagedMqttClientOptions _managedMqttClientOptions;
    private MqttClientOptions _mqttClientOptions;
    private MqttClientCredentialOptions _mqttClientCredentials;
    private IServiceScopeFactory _serviceScopeFactory;
    private Common.Logging.ILogger _ILogger;
    private readonly IHubContext<IotHub> _iotHubContext;
    private readonly IthresholdSetting _thresholdSetting;
   


    public MqttMeasurementService(
        MqttClientCredentialOptions mqttClientCredentials,
        IServiceScopeFactory serviceScopeFactory,
        Common.Logging.ILogger ILogger,
        IHubContext<IotHub> iotHubContext,
        IthresholdSetting ithresholdSetting
      
       )
    {
        this._mqttClientCredentials = mqttClientCredentials ?? throw new ArgumentNullException(nameof(mqttClientCredentials));
        this._serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        this._ILogger = ILogger;
        this._iotHubContext = iotHubContext;
        this._thresholdSetting = ithresholdSetting;
      

        var clientId = _mqttClientCredentials.ClientId;
        var userName = _mqttClientCredentials.UserName;
        var password = _mqttClientCredentials.Password;
        var host = _mqttClientCredentials.Host;
        var port = _mqttClientCredentials.Port;

        this._mqttClientOptions = new MqttClientOptionsBuilder()
            .WithClientId(clientId)
            .WithTcpServer(host, port)
            .WithCredentials(userName, password)
            .WithCleanSession()
            .Build();

        _managedMqttClientOptions = new ManagedMqttClientOptionsBuilder()
            .WithClientOptions(_mqttClientOptions)
            .Build();
    }

    private async Task SendMeasurementSignalChange(string? panelId, string? deviceId, string tenantName)
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

    public async Task InitializeAsync()
    {
        var mqttFactory = new MqttFactory();

        _managedMqttClient = mqttFactory.CreateManagedMqttClient();

        _managedMqttClient.ConnectedAsync += async (args) =>
        {
            await _managedMqttClient.SubscribeAsync("+/+/measurement");
            await _managedMqttClient.SubscribeAsync("+/+/water-meter");
            await _managedMqttClient.SubscribeAsync("+/tracking-gps");
        };

        _managedMqttClient.DisconnectedAsync += async (args) =>
        {
            Console.WriteLine("DisconnectedAsync");
        };

        _managedMqttClient.ConnectingFailedAsync += async (args) =>
        {
            Console.WriteLine("ConnectingFailedAsync");
        };

        _managedMqttClient.ApplicationMessageReceivedAsync += HandlerReceivedMessage;

        await Task.CompletedTask;
    }

    public async Task ConnectAsync()
    {
        if (!_managedMqttClient.IsConnected)
        {
            await _managedMqttClient.StartAsync(_managedMqttClientOptions);
        }
    }

    public async Task Dispose()
    {
        _managedMqttClient.Dispose();
    }

    private string GetDataType(string topic)
    {
        try
        {
            return topic.Split('/')[2];
        }
        catch (Exception ex)
        {
            return string.Empty;
        }
    }

    private string getDatabase(string topic)
    {
        try
        {
            return topic.Split('/')[0];
        }
        catch (Exception ex)
        {
            return string.Empty;
        }
    }

    private string getDeviceId(string topic)
    {
        try
        {
            return topic.Split('/')[1];
        }
        catch (Exception ex)
        {
            return string.Empty;
        }
    }

    private bool isTrackingGps(string topic)
    {
        var list = topic.Split("/");
        if (list.Length == 2 && list[1] == "tracking-gps")
        {
            return true;
        }
        return false;
    }

    private async Task<T> Deserialize<T>(byte[] param)
    {
        using (MemoryStream ms = new MemoryStream(param))
        {
            return await JsonSerializer.DeserializeAsync<T>(ms);
        }
    }

    public async Task HandlerReceivedMessage(MqttApplicationMessageReceivedEventArgs args)
    {
      

        // handle message received
        try
        {
            //handle measurement = show/123456/measurement
            var database = getDatabase(args.ApplicationMessage.Topic);
            var deviceId = getDeviceId(args.ApplicationMessage.Topic);
            var dataType = GetDataType(args.ApplicationMessage.Topic);

            string hexString = Convert.ToHexString(args.ApplicationMessage.Payload);

            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var iMongodbClient = scope.ServiceProvider.GetRequiredService<IMongoClient>();

                var measurementRepo = new Repository<BsonDocument>(database, "Measurement", iMongodbClient);

                var auditRepo = iMongodbClient.GetDatabase(database).GetCollection<Audit>(
                "Audit", new MongoCollectionSettings());
                // Dung Tran

                var tenantSettingRepo = new Repository<BsonDocument>(database, "TenantSetting", iMongodbClient);

                var alarmRepo = new Repository<BsonDocument>(database, "Alarm", iMongodbClient);

       
                //_repositoryFactory.GetRepository<AlarmCommon>("Alarm");




                ///////////// handle tracking gpc /////////////////////
                if (isTrackingGps(args.ApplicationMessage.Topic))
                {
                    var trackingGpsPayload = args.ApplicationMessage.Payload;

                    var json = Encoding.UTF8.GetString(trackingGpsPayload);
                    var trackinGpsModel = JsonSerializer.Deserialize<CreateTrackingGpsModel>(json);

                    await auditRepo.InsertOneAsync(new Common.MongoDb.Audits.Audit()
                    {
                        Type = AuditType.TrackingGps,
                        AfterValue = !string.IsNullOrEmpty(json) ? BsonDocument.Parse(JsonSerializer.Serialize(trackinGpsModel)) : BsonNull.Value,
                    });

                    await auditRepo.InsertOneAsync(new Audit()
                    {
                        Type = "Mqtt_Payload_Tracking_GPS",
                        CollectionName = "Audit"
                    });

                    return;
                }


                switch (dataType)
                {
                    case "measurement":
                        {
                            var payloadAirQuality = ParseHexAirQuality(hexString, deviceId);

                            var entityAirQuality = BsonDocument.Parse(payloadAirQuality.Item1);
                            var entityHumidityQuality = BsonDocument.Parse(payloadAirQuality.Item2);
                            var entityTemperatureQuality = BsonDocument.Parse(payloadAirQuality.Item3);

                            await measurementRepo.InsertAsync(entityAirQuality);
                            await measurementRepo.InsertAsync(entityHumidityQuality);
                            await measurementRepo.InsertAsync(entityTemperatureQuality);
                            // Fixed issue 16579
                             
                           await _thresholdSetting.CheckThresholdSetting(entityAirQuality, database, tenantSettingRepo, alarmRepo, deviceId);

                           //
                           await auditRepo.InsertOneAsync(new Audit()
                            {
                                Type = "Mqtt_Payload_AIQ_" + deviceId,
                                CollectionName = "Measurement"
                            });

                            await SendMeasurementSignalChange(string.Empty, deviceId, database);
                            break;
                        }
                    case "water-meter":
                        {
                            var payload = ParseWaterMeter(hexString, deviceId);
                            await measurementRepo.InsertAsync(BsonDocument.Parse(payload));

                            string tracingWaterMetter = "Hex string get at: " + DateTime.UtcNow.ToString() + "\n";

                            _ILogger.Trace(tracingWaterMetter + hexString);

                            await auditRepo.InsertOneAsync(new Audit()
                            {
                                Type = "Mqtt_Payload_Water_Meter_" + deviceId,
                                CollectionName = "Measurement"
                            });

                            await SendMeasurementSignalChange(string.Empty, deviceId, database);
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
            }

        }
        catch (Exception ex)
        {
            throw new Exception("Can't handle received message!");
        }
    }

    private (string, string, string) ParseHexAirQuality(string hexString, string deviceId)
    {
        try
        {
            var time = DateTimeOffset.UtcNow.ToString();

            // Air quality Measurement
            JObject objAirQuality = new JObject();
            objAirQuality["gtech_IAQ_001"] = new JObject();
            objAirQuality["gtech_IAQ_001"]["pm2_5"] = new JObject(new JProperty("value", (double)Convert.ToInt32(hexString.Substring(14, 4), 16)), new JProperty("unit", "μg/m3"));
            objAirQuality["gtech_IAQ_001"]["pm10"] = new JObject(new JProperty("value", (double)Convert.ToInt32(hexString.Substring(18, 4), 16)), new JProperty("unit", "μg/m3"));
            objAirQuality["gtech_IAQ_001"]["TVOC"] = new JObject(new JProperty("value", (double)Convert.ToInt32(hexString.Substring(22, 4), 16)), new JProperty("unit", "mg/m3"));
            objAirQuality["gtech_IAQ_001"]["CO2"] = new JObject(new JProperty("value", (double)Convert.ToInt32(hexString.Substring(26, 4), 16)), new JProperty("unit", "ppm"));
            objAirQuality["gtech_IAQ_001"]["CO"] = new JObject(new JProperty("value", (double)Convert.ToInt32(hexString.Substring(38, 4), 16)), new JProperty("unit", "ppm"));
            objAirQuality["gtech_IAQ_001"]["formaldehyde"] = new JObject(new JProperty("value", (double)Convert.ToInt32(hexString.Substring(42, 4), 16)), new JProperty("unit", "mg/kg-day"));
            objAirQuality["gtech_IAQ_001"]["oxygen_sensor"] = new JObject(new JProperty("value", (double)Convert.ToInt32(hexString.Substring(46, 4), 16)), new JProperty("unit", "%"));
            objAirQuality["time"] = new JObject(new JProperty("$date", time));
            objAirQuality["source"] = new JObject(new JProperty("id", deviceId));
            objAirQuality["type"] = "gtech_IAQ_001";

            var airQualityString = objAirQuality.ToString();

            JObject objHumidity = new JObject();
            objHumidity["gtech_Humidity_001"] = new JObject();
            objHumidity["gtech_Humidity_001"]["humidity"] = new JObject(new JProperty("value", (double)Convert.ToInt32(hexString.Substring(10, 4), 16) / 10.0), new JProperty("unit", "%"));
            objHumidity["time"] = new JObject(new JProperty("$date", time));
            objHumidity["source"] = new JObject(new JProperty("id", deviceId));
            objHumidity["type"] = "gtech_Humidity_001";

            var humidityString = objHumidity.ToString();

            JObject objTemperature = new JObject();
            objTemperature["gtech_Temperature_001"] = new JObject();
            objTemperature["gtech_Temperature_001"]["temperature"] = new JObject(new JProperty("value", (double)Convert.ToInt32(hexString.Substring(6, 4), 16) / 10.0), new JProperty("unit", "C"));
            objTemperature["time"] = new JObject(new JProperty("$date", time));
            objTemperature["source"] = new JObject(new JProperty("id", deviceId));
            objTemperature["type"] = "gtech_Temperature_001";

            var temperatureString = objTemperature.ToString();

            return (airQualityString, humidityString, temperatureString);
        }
        catch (Exception e)
        {
            throw new Exception($"Can't parse data from {hexString} string!");
        }
    }

   

    private void ParseHexData(string hexString)
    {

        // Chuyển đổi chuỗi hex thành mảng byte
        byte[] byteData = new byte[hexString.Length / 2];

        for (int i = 0; i < byteData.Length; i++)
        {
            byteData[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
        }

        // Khởi tạo đối tượng JSON và gán các giá trị
        var json = new
        {
            _id = ObjectId.GenerateNewId(),
            gt_IAQ_001 = new
            {
                temperature = new
                {
                    value = (double)BitConverter.ToInt16(new byte[] { byteData[3], byteData[2] }, 0) / 10,
                    unit = "C"
                },
                humidity = new
                {
                    value = (double)BitConverter.ToUInt16(new byte[] { byteData[5], byteData[4] }, 0) / 10,
                    unit = "%"
                },
                pm2_5 = new
                {
                    value = BitConverter.ToUInt16(new byte[] { byteData[7], byteData[6] }, 0),
                    unit = "ug/m3"
                },
                pm10 = new
                {
                    value = BitConverter.ToUInt16(new byte[] { byteData[9], byteData[8] }, 0),
                    unit = "ug/m3"
                },
                TVOC = new
                {
                    value = BitConverter.ToUInt16(new byte[] { byteData[11], byteData[10] }, 0),
                    unit = "ppb"
                },
                CO2 = new
                {
                    value = BitConverter.ToUInt16(new byte[] { byteData[13], byteData[12] }, 0),
                    unit = "ppm"
                },
                CO = new
                {
                    value = BitConverter.ToUInt16(new byte[] { byteData[15], byteData[14] }, 0),
                    unit = "ppm"
                },
                formaldehyde = new
                {
                    value = BitConverter.ToUInt16(new byte[] { byteData[17], byteData[16] }, 0),
                    unit = "ug/m3"
                },
                oxygen = new
                {
                    value = BitConverter.ToUInt16(new byte[] { byteData[19], byteData[18] }, 0),
                    unit = "%"
                }
            },
            time = DateTime.Now,
            source = new
            {
                id = "<uid>"
            },
            type = "gt_IAQ_001"
        };

        // Chuyển đổi đối tượng JSON thành chuỗi JSON và xuất ra màn hình
        string jsonString = JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(jsonString);
    }


    

    private string ParseWaterMeter(string drawDataString, string deviceId)
    {
        try
        {

            var time = DateTimeOffset.UtcNow.ToString();

            // Get the address, function code, and data length from the draw data string
            string address = drawDataString.Substring(0, 4);            // 0,4
            string functionCode = drawDataString.Substring(4, 2);       // 4, 2
            string dataLength = drawDataString.Substring(6, 2);         // 6, 2

            // Convert the data length from hexadecimal to decimal
            int dataLengthDecimal = Convert.ToInt32(dataLength, 16);

            // Create a list to hold the data values
            List<double> dataValues = new List<double>();

            // Loop through the data values in the draw data string
            string dataString = drawDataString.Substring(8);            // 8
            for (int i = 0; i < 10; i++)
            {
                
                int dataLengthForValue = i == 5 || i == 7 || i == 9 ? 4 : 8;        // is ok
                var dataValueString = dataString.Substring(0, dataLengthForValue);
                var hightlightedData = dataValueString;

                if (i == 0 || i == 1 || i == 2 || i == 3 || i == 4 || i == 8)                 //0,1,2,3,4, 6, 8
                {
                    hightlightedData = ConvertHightlineData(hightlightedData);
                }

                double dataValue = ConvertHexToSingle(hightlightedData);

                // Add the data value to the list
                dataValues.Add(dataValue);
                dataString = dataString.Substring(dataLengthForValue);
            }
            

            JObject jsonObject = new JObject();
            jsonObject["gtech_Water_001"] = new JObject();
            //jsonObject["Address"] = address;
            //jsonObject["FunctionCode"] = functionCode;
            //jsonObject["DataLength"] = dataLength;
            jsonObject["gtech_Water_001"]["insflow_sec"] = new JObject(new JProperty("value", dataValues[0]), new JProperty("unit", "m3/sec")) ;
            jsonObject["gtech_Water_001"]["insflow_min"] = new JObject(new JProperty("value", dataValues[1]), new JProperty("unit", "m3/min")); ;
            jsonObject["gtech_Water_001"]["insflow_hour"] = new JObject(new JProperty("value", dataValues[2]), new JProperty("unit", "m3/hour")); ;
            jsonObject["gtech_Water_001"]["velocity"] = new JObject(new JProperty("value", dataValues[3]), new JProperty("unit", "-")); ;
            jsonObject["gtech_Water_001"]["cumulativeflow"] = new JObject(new JProperty("value", dataValues[4]), new JProperty("unit", "-")); ; ;
            jsonObject["gtech_Water_001"]["Data6"] = dataValues[5];
            jsonObject["gtech_Water_001"]["Data7"] = dataValues[6];
            jsonObject["gtech_Water_001"]["minus_accumulativeflow"] = dataValues[7];
            jsonObject["gtech_Water_001"]["netcumulativeflow"] = new JObject(new JProperty("value", dataValues[8]), new JProperty("unit", "-")); ; ; ;
            jsonObject["gtech_Water_001"]["Data10"] = dataValues[9];
            jsonObject["time"] = new JObject(new JProperty("$date", time));

            jsonObject["source"] = new JObject(new JProperty("id", deviceId));
            jsonObject["type"] = "gtech_Water_001";

            string drawDataJson = jsonObject.ToString();

            return drawDataJson;
        }
        catch(Exception e)
        {
            return string.Empty;
        }
    }

    private string ConvertHightlineData(string hexString)
    {
        var secondPart = hexString.Substring(4);
        var firstPart = hexString.Substring(0,4);

        //var firstOfFirst = firstPart.Substring(0,2);
        //var secondOfFirst = firstPart.Substring(2);

        // Dung Tran fixed

        var firstOfFirst = secondPart.Substring(0,2);
        var secondOfFirst = secondPart.Substring(2);

        //


        return firstOfFirst + secondOfFirst + firstPart;
    }

    static double ConvertToDecimal(string hexString)
    {
        try
        {
            uint num = uint.Parse(hexString, NumberStyles.AllowHexSpecifier);
            float result = BitConverter.ToSingle(BitConverter.GetBytes(num), 0);
            return result;
        }
        catch(Exception e)
        {
            return 0.0;
        }
    }

    public static double HexToDecimal(string hexString)
    {
        uint hex = uint.Parse(hexString, System.Globalization.NumberStyles.HexNumber);
        byte[] bytes = BitConverter.GetBytes(hex);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        float f = BitConverter.ToSingle(bytes, 0);
        return (double)f;
    }

    public static string HexToIEEE754(string hexString)
    {
        // Chuyển đổi chuỗi hexa sang số nguyên không dấu 32-bit
        uint hex = uint.Parse(hexString, System.Globalization.NumberStyles.HexNumber);

        // Chuyển đổi số nguyên không dấu sang số nhị phân
        string binary = Convert.ToString(hex, 2).PadLeft(32, '0');

        // Tách các phần của số nhị phân
        string signBit = binary.Substring(0, 1);
        string exponent = binary.Substring(1, 8);
        string significand = binary.Substring(9);

        // Gộp các phần lại thành số nhị phân 32-bit
        string ieee754Binary = signBit + exponent.PadLeft(8, '0') + significand.PadRight(23, '0');

        // Chuyển đổi số nhị phân 32-bit sang chuỗi hexa
        uint ieee754Hex = Convert.ToUInt32(ieee754Binary, 2);
        string ieee754HexString = ieee754Hex.ToString("X8");

        return ieee754HexString;
    }

    public static string BinaryStringToHexString(string binary)
    {
        if (string.IsNullOrEmpty(binary))
            return binary;

        StringBuilder result = new StringBuilder(binary.Length / 8 + 1);

        // TODO: check all 1's or 0's... throw otherwise

        int mod4Len = binary.Length % 8;
        if (mod4Len != 0)
        {
            // pad to length multiple of 8
            binary = binary.PadLeft(((binary.Length / 8) + 1) * 8, '0');
        }

        for (int i = 0; i < binary.Length; i += 8)
        {
            string eightBits = binary.Substring(i, 8);
            result.AppendFormat("{0:X2}", Convert.ToByte(eightBits, 2));
        }

        return result.ToString();
    }

    private static double ConvertHexToSingle(string hexVal)
    {
        try
        {
            int i = 0, j = 0;
            byte[] bArray = new byte[4];
            for (i = 0; i <= hexVal.Length - 1; i += 2)
            {
                bArray[j] = Byte.Parse(hexVal[i].ToString() + hexVal[i + 1].ToString(), System.Globalization.NumberStyles.HexNumber);
                j += 1;
            }
            Array.Reverse(bArray);
            float f = BitConverter.ToSingle(bArray, 0);
            return (double)f;
        }
        catch (Exception ex)
        {
            throw new FormatException("The supplied hex value is either empty or in an incorrect format.  Use the " +
                "following format: 00000000", ex);
        }
    }


}
