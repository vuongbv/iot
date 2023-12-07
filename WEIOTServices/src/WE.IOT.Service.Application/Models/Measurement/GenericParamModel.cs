using WE.IOTService.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WE.IOT.Service.Application.Constants;

namespace WE.IOT.Service.Application.Models.Measurement
{
    public class GenericParamModel
    {
        public string? SourceId { get; set; }
        public string? SubId { get; set; }
        public string DataPoint { get; set; }
        public string? DataPath { get; set; }
        public DataConstants.DataResultType? DataResultType { get; set; }
        public DataConstants.DataAggregateType? DataAggregateType { get; set; }
        public DataConstants.Periodicity? Periodicity { get; set; }
        public int? TimeOffset { get; set; } = 8;
        public string? Formular { get; set; }
    }
    public class MultiChartGenericParamModel : GenericParamModel
    {
        public string WidgetId { get; set; }
    }
}
