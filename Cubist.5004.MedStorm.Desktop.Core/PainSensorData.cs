using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSSApplication.Core
{
    public struct PainSensorData
    {
        public string Timestamp { get; set; }
        public int Pain { get; set; }
        public int Awakening { get; set; }
        public int Nerveblock { get; set; }
        public int BadSignal { get; set; }
        public double[] SkinCond { get; set; }
        public PainSensorData(string timestamp, int pain, int awakening, int nerveblock, int badSignal, double[] skinCond)
        {
            Timestamp = timestamp;
            Pain = pain;
            Awakening = awakening;
            Nerveblock = nerveblock;
            BadSignal = badSignal;
            SkinCond = skinCond;
        }
    }
}
