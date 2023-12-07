using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WE.IOT.Service.Application.Constants;
using WE.IOTService.Domain.Entities;

namespace WE.IOT.Service.Application.Models.Measurement
{
    public class SearchGenericSingleValueViewModel
    {
        public string? Id { get; set; }
        public string? SubId { get; set; }
        public bool? IsSum { get; set; }
        public bool? IsAvg { get; set; }
        public DataConstants.Periodicity? Periodicity { get; set; }
    }
}
