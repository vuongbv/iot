using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.Models;

namespace WE.IOT.Service.Application.Models.Device
{
    public class SearchDeviceViewModel:SearchBaseViewModel
    {
        public string? RouterId { get; set; }
        public string? InterfaceId { get; set; }
        public string? DeviceId { get; set; }
        public string? DeviceName { get; set;}
    }
    public class ExportDeviceModel
    {
        public string DeviceId { get; set; }
        public string DataType { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}
