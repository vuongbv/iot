using WE.IOT.Service.Application.Models.TenantSetting;
using WE.IOT.Service.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Dtos.TenantSetting
{
    public class CreateOrUpdateThresholdSettingDTO
    {
        public string? Id { get; set; }
        public string? Criteria { get; set; } = default!;
        public string? Device { get; set; } = default!;
        public string? DeviceName { get; set; }
        public double? ValueFrom { get; set; }
        public double? ValueTo { get; set; }
        public string? ValueEqual { get; set; }
        public Operator? Operator { get; set; }
        public List<WE.IOT.Service.Application.Models.TenantSetting.Action>? Actions { get; set; } = default!;
        public CreateOrUpdateThresholdSettingModel Value { get; set; } 
        public string? Geometry { get; set; }
    }

    
   
}
