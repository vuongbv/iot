using Microsoft.AspNetCore.Http;
using WE.IOT.Service.Domain.Entities;
namespace WE.IOT.Service.Application.Models.TenantSetting;



public class ThresholdSettingModel
{
    public string? Criteria { get; set; } = default!;
    public string? Device { get; set; } = default!;
    public double? ValueFrom { get; set; }
    public double? ValueTo { get; set; }
    public string? ValueEqual { get; set; }
    public Operator? Operator { get; set; }
    public List<Action>? Actions { get; set; }
    public string? Geometry { get; set; }

}

public class CreateOrUpdateThresholdSettingModel
{
    public string? Criteria { get; set; } = default!;
    public string? Device { get; set; } = default!;
    public double? ValueFrom { get; set; }
    public double? ValueTo { get; set; }
    public string? ValueEqual { get; set; }
    public Operator? Operator { get; set; }
    public List<Action>? Actions { get; set; }
    public string? Geometry { get; set; }


}


public enum SeverityType
{
    Critical,
    Major,
    Minor
}
public enum StatusType
{
    Active,
    Major,
    Minor
}
public class Action
{
    public ActionType? Type { get; set; }
    public string? Description { get; set; }
    public SeverityType SeverityType { get; set; }
    public string? Recipient { get; set; }
    public string? AlarmType { get; set; }
    public DateTime? LastTrigger { get; set; }
    public int? Frequency { get; set; }
}
public enum ActionType
{
    SMS,
    Email,
    Alarm,
    UpdateDeviceStatus,
}