using Microsoft.AspNetCore.Mvc;
using WE.IOT.Service.Application.Models.Measurement;
using WE.IOT.Service.Application.Services;
using System.Linq;
using WE.IOT.Service.Application.Dto.Measurement;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using WE.IOT.Service.Application.Dtos.Measurement;
using Common.MongoDbMultitenant;
using Common.Mongodb.MultiTenant;
using Common.ControllerBase;
using Newtonsoft.Json.Linq;
using System.Text.Json;
using WE.IOTService.Domain.Entities;
using WE.IOT.Service.Application.Models.Device;
using Azure.Identity;
using System.Collections;
using MongoDB.Driver.Linq;
using System.Data;
using ClosedXML.Excel;
using System.IO.Compression;
using DocumentFormat.OpenXml.EMMA;
using WE.IOT.Service.Application.Constants;
using Common.Mongodb.Collections;
using Microsoft.AspNetCore.Http.Extensions;

namespace WE.IOT.Service.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MeasurementController : ApiControllerBase
    {
        private readonly IMeasurementServices _measurementService;
        private readonly IMultiTenantService<Common.Mongodb.MultiTenant.Tenant> _tenantService;
        private readonly IDeviceService _deviceService;
        private readonly IDeviceStatusService _deviceStatusService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly HttpClient _client;

        public MeasurementController(
            IMeasurementServices measurementService,
            IMultiTenantService<Common.Mongodb.MultiTenant.Tenant> tenantService,
            IDeviceService deviceService,
            IDeviceStatusService deviceStatusService,
            IHttpContextAccessor httpContextAccessor,
            HttpClient client
            )
        {
            this._measurementService = measurementService ?? throw new ArgumentNullException(nameof(measurementService));
            this._tenantService = tenantService;
            this._deviceService = deviceService ?? throw new ArgumentNullException(nameof(deviceService));
            this._deviceStatusService = deviceStatusService ?? throw new ArgumentNullException(nameof(deviceStatusService));
            this._httpContextAccessor = httpContextAccessor;
            this._client = client;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _measurementService.GetAll());
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] dynamic payload)
        {
            var model = new CreateMeasurementModel()
            {
                Payload = JsonSerializer.Serialize(payload)
            };

            var validator = (new CreateMeasurementModelValidator()).Validate(model);

            if (!validator.IsValid)
            {
                return BadRequest(validator.Errors);
            }
            try
            {
                var result = await _measurementService.Create(model);
                return Ok(result);
            }catch(Exception ex)
            {
                return BadRequest(ex.Message);
            }

        }

        [HttpGet("search")]
        [Produces(typeof(List<dynamic>))]
        public async Task<IActionResult> Filter([FromQuery] FilterMeasurementModel model)
        {
            var result = new List<dynamic>();
            var tenant = await _tenantService.GetTenantAsync();

            //logic to check tenant
            if (tenant.Name.Equals("Watsons"))
            {
                result = await _measurementService.SearchForWatsons(model);
            }
            else
            {
                var queryParam = _httpContextAccessor.HttpContext.Request.GetDisplayUrl();

                var mongoPagedList = await _measurementService.SearchMeasurement(model);

                var pagedList = new PagedList<dynamic>(mongoPagedList, model.PageSize, model.PageIndex);

                if (pagedList.HasNextPage && queryParam.Contains($"page_index={model.PageIndex}"))
                {
                    var nextUrl = queryParam.Replace($"page_index={model.PageIndex}", $"page_index={model.PageIndex + 1}");
                    pagedList.Next = nextUrl;
                }

                if (pagedList.HasPreviousPage && queryParam.Contains($"page_index={model.PageIndex}"))
                {
                    var preUrl = queryParam.Replace($"page_index={model.PageIndex}", $"page_index={model.PageIndex - 1}");
                    pagedList.Previous = preUrl;
                }

                return Ok(pagedList);

            }

            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] dynamic payload)
        {
            var model = new UpdateMeasurementModel()
            {
                Payload = JsonSerializer.Serialize(payload)
            };
            var validator = (new UpdateMeasurementModelValidator()).Validate(model);

            if (!validator.IsValid)
            {
                return BadRequest(validator.Errors);
            }

            var result = await _measurementService.Update(id, model);

            if (result)
            {
                return Ok();
            }
            else
            {
                return BadRequest();
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            await _measurementService.Delete(id);
            return Ok();
        }

        [HttpGet("total-power")]
        [Produces(typeof(TotalPowerDto))]
        public async Task<IActionResult> GetPower([FromQuery] SearchPowerViewModel search)
        {

            var tenant = await _tenantService.GetTenantAsync();

            if(!(await IsActiveDevice(search.Id)))
            {
                return BadRequest("Device is inactive or notfound!");
            }

            //logic to check tenant
            if (tenant.Name.Equals("Watsons"))
            {
                var result = await _measurementService.GetPowerForWatsons(search.Id, search.ChannelNames, search.Periodicity);
                return Ok(result);
            }
            else
            {
                var result = await _measurementService.GetGenericPower(search.Id, search.SubId , (tenant != null?tenant.Name : string.Empty), search.Periodicity);
                return Ok(result);
            }

        }

        [HttpGet("battery")]
        [Produces(typeof(DeviceBatteryDto))]
        public async Task<IActionResult> GetBatteryIndicator([FromQuery] string id)
        {
            if (!(await IsActiveDevice(id)))
            {
                return BadRequest("Device is inactive or notfound!");
            }
            var result = await _measurementService.GetGenericBatteryIndicator(id);

            return Ok(result);
        }


        [HttpGet("temperature")]
        [Produces(typeof(TemperatureDto))]
        public async Task<IActionResult> GetTemperatureIndicator([FromQuery] string id)
        {
            if (!(await IsActiveDevice(id)))
            {
                return BadRequest("Device is inactive or notfound!");
            }

            var result = await _measurementService.GetTemperatureIndicator(id);

            return Ok(result);
        }

        [HttpGet("fiveg")]
        [Produces(typeof(FiveGDto))]
        public async Task<IActionResult> GetFiveGIndicator([FromQuery] string id)
        {
            if (!(await IsActiveDevice(id)))
            {
                return BadRequest("Device is inactive or notfound!");
            }

            var result = await _measurementService.GetFiveGIndicator(id);

            return Ok(result);
        }

        [HttpGet("powerhourly")]
        [Produces(typeof(PowerSunlightGeneratedHourlyDto))]
        public async Task<IActionResult> GetPowerHourly([FromQuery] string id, string? refId)
        {
            if (!(await IsActiveDevice(id)))
            {
                return BadRequest("Device is inactive or notfound!");
            }
            // Starting the Stopwatch
            var result = await _measurementService.GetPowerGeneratedHourly(id, refId);
            return Ok(result);
        }

        [HttpGet("fivegsignalhourly")]
        [Produces(typeof(List<SingleChartQueryResult>))]
        public async Task<IActionResult> GetFiveGSignalStrengHourly([FromQuery] string id)
        {
            if (!(await IsActiveDevice(id)))
            {
                return BadRequest("Device is inactive or notfound!");
            }
            // Starting the Stopwatch
            var result = await _measurementService.GetFiveGSignalHourly(id);
            return Ok(result);
        }

        [HttpGet("singlechartdata")]
        [Produces(typeof(List<SingleChartDto>))]
        public async Task<IActionResult> GetChartData([FromQuery] GenericParamModel model)
        {
            if (!string.IsNullOrEmpty(model.SourceId))
            {
                if (!(await IsActiveDevice(model.SourceId)))
                {
                    return BadRequest("Device is inactive or notfound!");
                }
            }


            var results = new List<SingleChartDto>();
            if (model.DataPoint.Equals("5GDeviceSignal"))
            {
                if (model.DataResultType == DataConstants.DataResultType.Hourly)
                {
                    var data = await _measurementService.GetFiveGSignalHourly(model.SourceId);
                    results = data.Select(x => new SingleChartDto()
                    {
                        Point = ConvertTime(model.TimeOffset.Value, x._id.Value).ToString(),
                        Value = x.Value
                    }).ToList();
                }
            }
            else
            {
                results = await _measurementService.GetGenericSingleChartValue(model);
            }


            return Ok(results);
        }
        [HttpGet("singlevaluedata")]
        [Produces(typeof(SingleValueDto))]
        public async Task<IActionResult> GetSingleValueData([FromQuery] GenericParamModel model)
        {
            if (!string.IsNullOrEmpty(model.SourceId))
            {
                if (!(await IsActiveDevice(model.SourceId)))
                {
                    return BadRequest("Device is inactive or notfound!");
                }
            }
            var data = await _measurementService.GetGenericSingleValue(model);
            var results = data;


            return Ok(results);
        }

        [HttpGet("export")]
        [Produces(typeof(FileContentResult))]
        public async Task<IActionResult> Export([FromQuery] FilterMeasurementModel model)
        {
            if (!(await IsActiveDevice(model.Id)))
            {
                return BadRequest("Device is inactive or notfound!");
            }

            var startdate = model.StartDate.HasValue ? model.StartDate.Value.ToString("_ddMMyyyy.HHmm") : string.Empty;
            var enddate = model.EndDate.HasValue ? model.EndDate.Value.ToString("_ddMMyyyy.HHmm") : string.Empty;
            var result = new List<string>();
            var tenant = await _tenantService.GetTenantAsync();
            //logic to check tenant
            if (tenant.Name.Equals("Watsons"))
            {
                //TODO: update format for watson 
                //result = await _measurementService.SearchForWatsons(model);
            }
            else
            {
                using (XLWorkbook wb = new XLWorkbook())
                {
                    result = await _measurementService.ExportGeneric(model);
                    if (result.Any())
                    {
                        var groups = result.Select(x => DeserializeAndFlatten(x)).ToList().GroupBy(i => new { V = i.GetValueOrDefault("type"), i.Values.Count });
                        int i = 0;
                        foreach (var group in groups)
                        {
                            if (groups.Where(g => g.Key.V.Equals(group.Key.V)).Count() > 1)
                            {
                                i++;
                            }
                            var items = group.ToList();
                            DataTable dt = new DataTable("_" + group.Key.V.ToString() + (i == 0 ? string.Empty : "_" + i));
                            foreach (var prop in items[0])
                            {
                                if (!prop.Key.ToString().Equals("_id"))
                                {
                                    dt.Columns.Add(prop.Key.ToString());
                                }
                            }
                            foreach (var item in items)
                            {
                                item.Remove("_id");
                                item["time"] = DateTime.Parse(item["time"].ToString()).AddHours(8);
                                dt.Rows.Add(item.Values.ToArray());
                            }


                            wb.Worksheets.Add(dt);

                        }
                    }
                    else
                    {
                        DataTable dt = new DataTable("_Empty");
                        wb.Worksheets.Add(dt);
                    }

                    using (MemoryStream stream = new MemoryStream())
                    {
                        wb.SaveAs(stream);
                        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", model.Id + startdate + enddate + "_Measurement_Data.xlsx");
                    }
                }

            }
            return Ok(result);
        }

        [HttpGet("fetchrouterstatus")]
        public async Task<IActionResult> FetchRouterStatus([FromQuery] string devices)
        {
            if (!string.IsNullOrEmpty(devices))
            {
                try
                {
                    await _measurementService.GetDataRouterStatus(devices);
                }catch(Exception ex)
                {
                    BadRequest(ex.Message);
                }
            }
            return Ok();
        }

        [HttpGet("fetchroutersignal")]
        public async Task<IActionResult> FetchRouterSignal([FromQuery] string devices)
        {
            if (!string.IsNullOrEmpty(devices))
            {
                try
                {
                    await _measurementService.GetDataRouterSignal(devices);
                }
                catch (Exception ex)
                {
                    BadRequest(ex.Message);
                }
            }
            return Ok();
        }
        [HttpGet("fetchrouteruptimestate")]
        public async Task<IActionResult> FetchRouterUptimeState([FromQuery] string devices)
        {
            if (!string.IsNullOrEmpty(devices))
            {
                try
                {
                    await _measurementService.GetDataRouterUptimeState(devices);
                }
                catch (Exception ex)
                {
                    BadRequest(ex.Message);
                }
            }
            return Ok();
        }
        [HttpGet("fetchrouter5gtest")]
        [AllowAnonymous]
        public async Task<IActionResult> FetchRouter5GTest([FromQuery] string devices, string? accountid, string? host, string? testtype)
        {

            string url = HttpContext.Request.GetDisplayUrl();
            url = url.Replace("fetchrouter5gtest", "fetchrouter5gtestapi");

            _client.GetAsync(url);

            return Ok();

            if (!string.IsNullOrEmpty(devices))
            {
                try
                {
                    Task.Factory.StartNew(() => FetchRouter5GTestAPI(devices, accountid, host, testtype));

                    //var result = await _measurementService.GetDataRouterSpeedTest(devices, accountid, host, testtype);
                    //if (result)
                    //{
                    //    return Ok();
                    //}

                    return Ok();

                }
                catch (Exception ex)
                {
                    BadRequest(ex.Message);
                }
            }
            return BadRequest();
        }

        [HttpGet("fetchrouter5gtestapi")]
        [AllowAnonymous]
        public async Task<IActionResult> FetchRouter5GTestAPI( string devices, string? accountid, string? host, string? testtype)
        {


            if (!string.IsNullOrEmpty(devices))
            {
                try
                {
                   // Task.Factory.StartNew(() => _measurementService.GetDataRouterSpeedTest(devices, accountid, host, testtype));

                    var result = await _measurementService.GetDataRouterSpeedTest(devices, accountid, host, testtype);
                    if (result)
                    {
                        return Ok();
                    }

                    return Ok();

                }
                catch (Exception ex)
                {
                    BadRequest(ex.Message);
                }
            }
            return BadRequest();
        }

        [HttpGet("getmultichartdata")]
        [Produces(typeof(List<MultiChartDto>))]
        [AllowAnonymous]
        public async Task<IActionResult> GetMultiChartData([FromQuery] MultiChartGenericParamModel model)
        {
            if (!string.IsNullOrEmpty(model.SourceId))
            {
                if (!(await IsActiveDevice(model.SourceId)))
                {
                    return BadRequest("Device is inactive or notfound!");
                }
            }

            var results = await _measurementService.GetGenericMultiChartValue(model);
            return Ok(results);
        }
        #region private functions
        private static Dictionary<string, object> DeserializeAndFlatten(string json)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            JToken token = JToken.Parse(json);
            FillDictionaryFromJToken(dict, token, "");
            return dict;
        }

        private static void FillDictionaryFromJToken(Dictionary<string, object> dict, JToken token, string prefix)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    foreach (JProperty prop in token.Children<JProperty>())
                    {
                        FillDictionaryFromJToken(dict, prop.Value, Join(prefix, prop.Name));
                    }
                    break;

                case JTokenType.Array:
                    int index = 0;
                    foreach (JToken value in token.Children())
                    {
                        FillDictionaryFromJToken(dict, value, Join(prefix, index.ToString()));
                        index++;
                    }
                    break;

                default:
                    dict.Add(prefix, ((JValue)token).Value);
                    break;
            }
        }

        private static string Join(string prefix, string name)
        {
            return (string.IsNullOrEmpty(prefix) ? name : prefix + "." + name);
        }

        private static int ConvertTime(int? timeOffset, int hour)
        {
            var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);

            return (hour + (timeOffset.HasValue ? timeOffset.Value : offset.Hours)) % 24;
        }

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
        #endregion
    }
}
