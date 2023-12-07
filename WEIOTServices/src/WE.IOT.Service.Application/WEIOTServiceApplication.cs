using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOTService.Domain.BussinesHandler
{
    public sealed class WEIOTServiceApplication
    {
        public static readonly Assembly Assembly = typeof(WEIOTServiceApplication).Assembly;
    }
}
