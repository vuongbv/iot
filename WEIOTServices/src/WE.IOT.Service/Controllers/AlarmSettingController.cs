using Common.ControllerBase;
using Common.Mongodb.Collections;
using Common.SMS.Sender;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WE.IOT.Service.Application.Models.AlarmSetting;
using WE.IOT.Service.Application.Models.TenantSetting;
using WE.IOT.Service.Application.Services;
using WE.IOTService.Application.Dto.AlarmSetting;
using WE.IOTService.Application.Dto.TenantSetting;

namespace WE.IOT.Service.Controllers
{

    [ApiController]
    [Route("[controller]")]
    public class AlarmSettingController : ApiControllerBase
    {
        private readonly IAlarmSettingService _alarmSettingService;
        private readonly ITenantSettingService _tenantSettingService;

        public AlarmSettingController(IAlarmSettingService alarmSettingService, ITenantSettingService tenantSettingService)
        {
            this._alarmSettingService = alarmSettingService ?? throw new ArgumentNullException(nameof(alarmSettingService));
            _tenantSettingService = tenantSettingService ?? throw new ArgumentNullException(nameof(tenantSettingService));
        }

        [HttpGet]
        [Produces(typeof(PagedList<AlarmSettingDto>))]
        public async Task<IActionResult> GetAll([FromQuery]SearchAlarmSettingViewModel searchModel)
        {
            var result = await this._alarmSettingService.GetAllAlarmSetting(searchModel);

            return Ok(result);
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Create([FromBody] CreateAlarmSettingModel model)
        {
            var validator = new CreateAlarmSettingModelValidator().Validate(model);

            if (!validator.IsValid)
            {
                return BadRequest(validator.Errors);
            }

            var result = await _alarmSettingService.CreateAlarmSetting(model);

            return Ok(result);
        }

        [HttpPut("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateAlarmSettingModel model)
        {
            var validator = new UpdateAlarmSettingModelValidator().Validate(model);

            if (!validator.IsValid)
            {
                return BadRequest(validator.Errors);
            }

            var result = await _alarmSettingService.UpdateAlarmSetting(id,model);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var result = await _alarmSettingService.DeleteAlarmSetting(id);
            return Ok(result);
        }

        [HttpGet("{id}")]
        [Produces(typeof(AlarmSettingDto))]
        public async Task<IActionResult> GetAlarmSetting(string id)
        {
            var result = _alarmSettingService.GetAlarmSetting(id);
            return Ok(result);
        }

        #region TenantSetting
        [HttpDelete("tariff/{id}")]
        public async Task<IActionResult> DeleteTenantSetting(string id)
        {
            var result = await _tenantSettingService.DeleteTenantSetting(id, string.Empty);
            return Ok(result);
        }

        [HttpGet("tariff")]
        [Produces(typeof(TenantSettingDto))]
        public async Task<IActionResult> GetTenantSetting()
        {
            var result = _tenantSettingService.GetTenantSetting();
            return Ok(result);
        }


        [HttpPut("tariff")]
        [AllowAnonymous]
        public async Task<IActionResult> UpdateTenantSetting([FromBody] UpdateTenantSettingModel model)
        {
            var result = await _tenantSettingService.UpdateTenantSetting(model);
            return Ok(result);
        }

        [HttpPost("tariff")]
        [AllowAnonymous]
        public async Task<IActionResult> CreateTenantSetting([FromBody] CreateTenantSettingModel model)
        {
            
            var result = await _tenantSettingService.CreateTenantSetting(model);

            return Ok(result);
        }
        #endregion
    }
}
