using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Models.Device;

public class CreateTrackingGpsModel
{
    public string source_id { get; set; }
    public string longitude { get; set; }
    public string latitude { get; set; }
}
