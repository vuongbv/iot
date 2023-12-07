using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Dtos.Measurement
{
    public class SingleChartDto
    {
        public string Point { get; set; }
        public double? Value { get; set; }
    }

    public class SingleValueDto
    {
        public double? Value {get;set; } 
        public string? Id { get; set; }
        public DateTime? Time { get; set; }
    }
    public class MultiChartDto
    {
        public string? IndicatorName { get; set; } = "unknown";
        public string? Unit { get; set; } = string.Empty;
        public string? Color { get; set; } = "white";
        public List<SingleChartDto> Values { get; set; }
    }
    public class SingleIdDto
    {
        public string? Id { get; set; }
        public DateTime? Time { get; set; }
    }
}
