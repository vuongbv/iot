using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Constants;

public static class AlarmConstants
{
    public const string Active = "active";
    public const string Resolved = "resolved";
    public const string Acknowledged = "acknowledged";

    public enum RecepientType
    {
        SMS,
        EMAIL,
    }
}

