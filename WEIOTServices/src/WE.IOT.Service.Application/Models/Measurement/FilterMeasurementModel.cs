using Common.Models;
using MongoDB.Queries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WE.IOT.Service.Application.Constants;
using WE.IOTService.Domain.Entities;

namespace WE.IOT.Service.Application.Models.Measurement
{
    public class FilterMeasurementModel
    {
        public string? Id { get; set; }
        public string? DataType { get; set; }
        public DataConstants.Periodicity Periodicity { get; set; }
        public DateTime? StartDate { get; set; } = DateTime.UtcNow.AddDays(-7);
        public DateTime? EndDate { get; set; } = DateTime.UtcNow;
        public int? PageSize { get; set; } = 20;
        public int? PageIndex { get; set; } = 1;
    }

}
