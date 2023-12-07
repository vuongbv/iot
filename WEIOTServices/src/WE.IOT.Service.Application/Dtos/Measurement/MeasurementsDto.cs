using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WE.IOT.Service.Application.Models.Measurement;

namespace WE.IOT.Service.Application.Dtos.Measurement
{
    public class WatsonsMeasurementDto
    {
        public DateTime Time { get; set; }

        public Mcx_pv Channel { get; set; }
        public Mcx_pv Channel1 { get; set; }
        public Mcx_pv Channel2 { get; set; }
        public Mcx_pv Channel3 { get; set; }
    }
}
