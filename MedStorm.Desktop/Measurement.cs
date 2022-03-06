using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Plot
{
    public struct Measurement
    {
        public double Value { get; set; }
        public DateTime TimeStamp { get; set; }
        public bool IsBadSignal { get; set; }
    }
}
