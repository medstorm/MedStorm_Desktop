using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSSApplication.Core
{
    public enum DataType
    {
        RawData,
        Comment,
        PatienID
    }
    public struct RawDataHeader
    {
        public DateTime Time { get; set; }
        public DataType DataType { get; set; }
    }
}
