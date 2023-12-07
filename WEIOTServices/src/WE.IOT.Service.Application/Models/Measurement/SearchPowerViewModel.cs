using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WE.IOT.Service.Application.Constants;
using WE.IOTService.Domain.Entities;

namespace WE.IOT.Service.Application.Models.Measurement
{
    public class SearchPowerViewModel
    {
        public string? Id { get; set; }
        public string? SubId { get; set; }
        public IEnumerable<string>? ChannelNames { get; set; }
        public DataConstants.Periodicity? Periodicity { get; set; }
    }

    public class MeasurementDate
    {
        public int Year { get; set; } = 1;
        public int Month { get; set; } = 1;
        public int Day { get; set; } = 1;
        public int Hour { get; set; } = 0;
        public int Minute { get; set; } = 0;
        public int Second { get; set; } = 0;
    }

    public static class MeasurementDateExtension
    {
        public static DateTime ToDateTime(this MeasurementDate model)
        {
            return new DateTime(model.Year, model.Month, model.Day, model.Hour, model.Minute, model.Second);
        }

      
    }
}
