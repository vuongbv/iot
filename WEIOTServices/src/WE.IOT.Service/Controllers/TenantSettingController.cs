using Common.ControllerBase;
using Common.Mongodb.Collections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WE.IOT.Service.Application.Constants;
using WE.IOT.Service.Application.Dtos.TenantSetting;
using WE.IOT.Service.Application.Models.TenantSetting;
using WE.IOT.Service.Application.Services;
using WE.IOTService.Application.Dto.TenantSetting;
using WE.IOT.Service.Domain.Entities;

namespace WE.IOT.Service.Controllers;

[ApiController]
[Route("[controller]")]
public class TenantSettingController : ApiControllerBase
{
    private readonly ITenantSettingService _tenantSettingService;

    public TenantSettingController(ITenantSettingService tenantSettingService)
    {
        this._tenantSettingService = tenantSettingService ?? throw new ArgumentNullException(nameof(tenantSettingService));
    }

    [HttpGet()]
    [Produces(typeof(object))]
    public async Task<IActionResult> GetSetting([FromQuery] GetTenantSettingModel model)
    {
        switch (model.Key)
        {
            case TenantSettingKeyConstants.LogoSettingKey:
                {
                    var logoBase64String = await _tenantSettingService.GetTenantLogo();
                    var byteArray = Convert.FromBase64String(logoBase64String);
                    return Ok(byteArray);
                }
            case TenantSettingKeyConstants.TariffSettingKey:
                {
                    var value = await _tenantSettingService.GetTarifSetting(model.TariffStartDate);

                    var tariffDto = new TenantSettingDto()
                    {
                        TariffValue = value.Value,
                        TariffUnit = value.Unit,
                        Id = value.Id,
                    };
                    return Ok(tariffDto);
                }
            default:
                break;
        }

        return NoContent();
    }

    [HttpGet("tariff")]
    [Produces(typeof(PagedList<TariffSettingDto>))]
    public async Task<IActionResult> GetTariffSetting([FromQuery] SearchTariffSettingModel model, string? tariffType)
    {

        var value = await _tenantSettingService.GetListTariffSetting(model, tariffType);

        var tariffResponseModel = value.Select(x => new TariffSettingDto()
        {
            Id = x.Id,
            DateTime = x.Month + "/" + x.Year,
            Value = x.Value,
            Unit = x.Unit
        });

        var paged = tariffResponseModel.ToPagedList(model.PageIndex, model.PageSize);

        return Ok(paged);

    }

    [HttpPost(), DisableRequestSizeLimit]
    [Produces(typeof(object))]
    [AllowAnonymous]
    public async Task<IActionResult> CreateOrUpdateSetting([FromQuery] string Key, [FromForm] CreateOrUpdateSettingModel model)
    {
        switch (Key)
        {
            case TenantSettingKeyConstants.LogoSettingKey:
                {
                    try
                    {
                        var file = model.FormFile;

                        if (file != null)
                        {
                            using (var ms = new MemoryStream())
                            {
                                file.CopyTo(ms);
                                var fileBytes = ms.ToArray();
                                string s = Convert.ToBase64String(fileBytes);
                                await _tenantSettingService.CreateLogoSetting(s);
                                return Ok(s);
                            }
                        }
                        else
                        {
                            return BadRequest();
                        }
                    }
                    catch (Exception ex)
                    {
                        return BadRequest();
                    }
                }
            default:
                {
                    var currentTime = DateTimeOffset.UtcNow;

                    var createTariffModel = new CreateTariffSettingModel()
                    {
                        yrmonth = currentTime.Year.ToString() + currentTime.Month.ToString(),
                        value = model.value.Value,
                        unit = model.unit,
                    };

                    await _tenantSettingService.CreateSetting(createTariffModel, Key);
                    return Ok();
                }
                break;
        }

        return NoContent();

    }

    [HttpPost("Threshold")]
    public async Task<IActionResult> CreateThresholdSetting([FromBody] CreateOrUpdateThresholdSettingModel model)
    {
        var result =  _tenantSettingService.CreateThresholdSetting(model);
        return Ok();
    }

    [HttpDelete("Threshold")]
    public async Task<IActionResult> DeleteThresholdSetting([FromQuery] string? id)
    {
        var result = _tenantSettingService.DeleteTenantSetting(id, string.Empty);
        return Ok();
    }

    [HttpPut("Threshold/{id}")]
    public async Task<IActionResult> UpdateThresholdSetting(string id, [FromBody] CreateOrUpdateThresholdSettingModel model)
    {
        var result = _tenantSettingService.UpdateThresholdSetting(id, model);
        return Ok();
    }

    [HttpPost("AlarmRecepient")]
    public async Task<IActionResult> CreateAlarmRecepient([FromBody] AlarmRecepient model)
    {
        var result = _tenantSettingService.CreateAlarmRecepient(model);
        return Ok();
    }

    [HttpPut("AlarmRecepient/{id}")]
    public async Task<IActionResult> UpdateAlarmRecepient(string id, [FromBody] AlarmRecepient model)
    {
        var result = _tenantSettingService.UpdateAlarmRecepient(id, model);
        return Ok();
    }
    [HttpGet("AlarmRecepient/Search")]
    [Produces(typeof(PagedList<AlarmRecepientDto>))]
    public async Task<IActionResult> SearchAlarmRecepient([FromQuery] AlarmRecepientSearch search)
    {
        var result = await _tenantSettingService.SearchAlarmRecepient(search);
        return Ok(result);
    }
    [HttpGet("AlarmRecepient")]
    [Produces(typeof(AlarmRecepient))]
    public async Task<IActionResult> GetAlarmRecepient([FromQuery] string id)
    {
        var result = await _tenantSettingService.GetAlarmRecepient(id);
        return Ok(result);
    }
    [HttpDelete("AlarmRecepient")]
    public async Task<IActionResult> DeleteAlarmRecepient([FromQuery] string? id)
    {
        var result = _tenantSettingService.DeleteTenantSetting(id, string.Empty);
        return Ok();
    }

    [HttpPost("TenantApplication")]
    public async Task<IActionResult> CreateTenantApplication([FromBody] TenantApplication model)
    {
        var result = _tenantSettingService.CreateTenantApplication(model);
        return Ok();
    }

    [HttpPut("TenantApplication")]
    public async Task<IActionResult> UpdateTenantApplication([FromBody] TenantApplication model)
    {
        var result = _tenantSettingService.UpdateTenantApplication(model);
        return Ok();
    }
    [HttpGet("TenantApplication")]
    [Produces(typeof(TenantApplication))]
    public async Task<IActionResult> GetTenantApplication()
    {
        var result = await _tenantSettingService.GetTenantApplication();
        return Ok(result);
    }
    [HttpDelete("TenantApplication")]
    public async Task<IActionResult> DeleteTenantApplication([FromQuery] string? id, string? key)
    {
        var result = _tenantSettingService.DeleteTenantSetting(id, TenantSettingKeyConstants.TenantApplicationKey);
        return Ok();
    }

    [HttpGet("Threshold")]
    [Produces(typeof(PagedList<CreateOrUpdateThresholdSettingDTO>))]
    public async Task<IActionResult> GetThreshold([FromQuery] TenantApplicationSearch search)
    {
        try
        {
            var result = await _tenantSettingService.GetThreshold(search);
            return Ok(result);
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    [HttpGet("Threshold/{id}")]
    [Produces(typeof(CreateOrUpdateThresholdSettingDTO))]
    public async Task<IActionResult> GetThresholdByKey(string id)
    {
        var result = await _tenantSettingService.GetThresholdByKey(id);
        return Ok(result);
    }
    [HttpPost("ChartStyleSetting")]
    public async Task<IActionResult> CreateChartStyleSetting([FromBody] ChartStyleSetting model)
    {
        var result = _tenantSettingService.CreateChartStyleSetting(model);
        return Ok();
    }

    [HttpPut("ChartStyleSetting")]
    public async Task<IActionResult> UpdateChartStyleSetting([FromBody] ChartStyleSetting model, string id)
    {
        var result = _tenantSettingService.UpdateChartStyleSetting(model, id);
        return Ok();
    }
    [HttpGet("ListChartStyleSetting")]
    [Produces(typeof(List<ChartStyleSettingDto>))]
    public async Task<IActionResult> GetListChartStyleSetting(string? chartTypeName)
    {
        var result = await _tenantSettingService.GetChartStyleSetting(chartTypeName);
        return Ok(result);
    }
    [HttpGet("ChartStyleSetting")]
    [Produces(typeof(ChartStyleSettingDto))]
    public async Task<IActionResult> GetChartStyleSetting(string? id)
    {
        var result = await _tenantSettingService.GetChartStyleSettingById(id);
        return Ok(result);
    }
    [HttpDelete("ChartStyleSetting")]
    public async Task<IActionResult> DeleteChartStyleSetting([FromQuery] string? id)
    {
        var result = _tenantSettingService.DeleteTenantSetting(id,string.Empty);
        return Ok();
    }

    [HttpGet("Power/Batch")]
    [Produces(typeof(BatchDeviceDto))]
    public async Task<IActionResult> GetPower()
    {
        var result = await _tenantSettingService.GetPowersSetting();
        
        return Ok(result);
    }

    [HttpPost("Power/Batch")]
    [Produces(typeof(PowersDto))]
    public async Task<IActionResult> CreatePower([FromBody] CreatePowerBatchModel model)
    {
        await _tenantSettingService.CreatePowersSetting(model);
        return Ok();
    }

    [HttpPost("leftmenusetting")]
    public async Task<IActionResult> CreateLeftMenuSetting([FromBody] LeftMenuSetting model)
    {
        var result = _tenantSettingService.CreateLeftMenuSetting(model);
        return Ok();
    }

    [HttpPut("leftmenusetting")]
    public async Task<IActionResult> UpdateLeftMenuSetting([FromBody] LeftMenuSetting model)
    {
        var result = _tenantSettingService.UpdateLeftMenuSetting(model);
        return Ok();
    }
    [HttpGet("leftmenusetting")]
    [Produces(typeof(LeftMenuSetting))]
    public async Task<IActionResult> GetLeftMenuSetting()
    {
        var result = await _tenantSettingService.GetLeftMenuSetting();
        return Ok(result);
    }
    [HttpDelete("leftmenusetting")]
    public async Task<IActionResult> DeleteLeftMenuSetting([FromQuery] string? id)
    {
        var result = _tenantSettingService.DeleteTenantSetting(String.Empty, TenantSettingKeyConstants.FeatureSettingKey);
        return Ok();
    }

}
