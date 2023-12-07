using Common.ControllerBase;
using Common.Mongodb.Collections;
using Common.Mongodb.Repository;
using Common.SMS.Sender;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver.GeoJsonObjectModel;
using MongoDB.Driver;
using WE.IOT.Service.Application.Models.Device;
using WE.IOT.Service.Application.Services;
using WE.IOTService.Application.Dto.Device;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using WE.IOT.Service.Application.BackgroundJobs;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.GeoJsonObjectModel;
using NetTopologySuite.Geometries;

namespace WE.IOT.Service.Controllers
{

    [ApiController]
    [Route("[controller]")]
    public class DeviceController : ApiControllerBase
    {
        private readonly IDeviceService _deviceService;
        private readonly ISmsSender _smsSender;

        public DeviceController(IDeviceService deviceService, ISmsSender _smsSender, IMongoClient iMongodbClient)
        {
            this._deviceService = deviceService ?? throw new ArgumentNullException(nameof(deviceService));
            this._smsSender = _smsSender ?? throw new ArgumentNullException(nameof(_smsSender));
        }

        [HttpGet]
        [Produces(typeof(PagedList<DeviceDto>))]
        public async Task<IActionResult> GetAll([FromQuery] SearchDeviceViewModel searchModel)
        {
            var result = await this._deviceService.GetAllDevice(searchModel);

            return Ok(result);
        }


        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDeviceModel model)
        {
            var validator = new CreateDeviceModelValidator().Validate(model);

            if (!validator.IsValid)
            {
                return BadRequest(validator.Errors);
            }

            var result = await _deviceService.CreateDevice(model);

            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateDeviceModel model)
        {
            var validator = new UpdateDeviceModelValidator().Validate(model);

            if (!validator.IsValid)
            {
                return BadRequest(validator.Errors);
            }

            var result = await _deviceService.UpdateDevice(id, model);
            return Ok(result);
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var result = await _deviceService.DeleteDevice(id);
            return Ok(result);
        }

        [HttpGet("{source_id}/source")]
        [Produces(typeof(DeviceDto))]
        public async Task<IActionResult> GetDeviceSource(string source_id)
        {
            var result = await _deviceService.GetDeviceSource(source_id);
            return Ok(result);
        }
        [HttpGet("{id}")]
        [Produces(typeof(DeviceDto))]
        public async Task<IActionResult> GetDevice(string id)
        {
            var result = _deviceService.GetDevice(id);
            return Ok(result);
        }
        [HttpPost("location")]
        public async Task<IActionResult> UpdateLocation([FromBody] UpdateLocationModel model)
        {
            var validator = new UpdateLocationModelValidator().Validate(model);

            if (!validator.IsValid)
            {
                return BadRequest(validator.Errors);
            }

            var result = _deviceService.UpdateLocation(model);

            return Ok(result);
        }
        [HttpGet("alldevicestatus")]
        [Produces(typeof(AllDeviceStatusDto))]
        public async Task<IActionResult> GetAllDeviceStatus()
        {
            var result = await this._deviceService.GetAllDeviceStatus();

            return Ok(result);
        }

        [HttpPost("extinfo")]
        public async Task<IActionResult> UpdateExtinfo([FromBody] UpdateExtinfoModel model)
        {
            var validator = new UpdateExtinfoModelValidator().Validate(model);

            if (!validator.IsValid)
            {
                return BadRequest(validator.Errors);
            }

            var result = _deviceService.UpdateExtinfo(model);

            return Ok(result);
        }

        [HttpPost("tracking-gps")]
        public async Task<IActionResult> TrackingGps([FromBody] CreateTrackingGpsModel model)
        {
            var result = await _deviceService.CreateTrackingGps(model);
            if (result) { return Ok(result); }
            return BadRequest(result);
        }
        [HttpGet("generateid")]
        public async Task<IActionResult> GenerateId()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();

            // Generate a string of length 9 with a mix of digits and letters
            var randomChars = Enumerable.Repeat(chars, 9)
                                         .Select(s => s[random.Next(s.Length)])
                                         .ToArray();

            // Choose 4 random positions in the string to replace with digits
            var positions = Enumerable.Range(0, 9).OrderBy(x => random.Next()).Take(4);

            // Replace the chosen positions with random digits
            foreach (var position in positions)
            {
                randomChars[position] = random.Next(10).ToString()[0];
            }
            return Ok(new string(randomChars));
        }

    }
}
