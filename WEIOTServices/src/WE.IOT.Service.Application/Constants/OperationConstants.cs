using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Constants;

public static class OperationConstants
{
    public const string TurnOff = "TurnOff";
    public const string TurnOn = "TurnOn";
    public const string StatusOn = "On";
    public const string StatusOff = "Off";

    public const string DeviceOperationType = "device_ops";

    public const string GeoFencingOprationType = "geo_gencing";

    public const string GeoFencingSmsOprationAction = "geo_sms_gencing";
    public const string GeoFencingEmailOprationAction = "geo_email_gencing";
    public const string GeoFencingAlarmOprationAction = "geo_alarm_gencing";

    public const string OperationProcessing = "Processing";
    public const string OperationProcessed = "Processed";
    public const string OperationCanceled = "Canceled";
    public const string OperationFailed = "Failed";
}

public enum OperationStatus
{
    Processing,
    Processed,
    Canceled,
    Failed
}
