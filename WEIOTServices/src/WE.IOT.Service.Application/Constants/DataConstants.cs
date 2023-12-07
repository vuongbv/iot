using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Constants
{
    public class DataConstants
    {
        public enum DataAggregateType
        {
            Min,
            Max,
            Avg,
            Sum,
            Current
        }

        public enum DataResultType
        {
            Record,
            RecordList,
            Hourly,
            Daily,
            Weekly,
            Monthly,
            Yearly
        }
        public enum Periodicity
        {
            daily,
            weekly,
            monthly,
            yearly,

        }
    }
}
