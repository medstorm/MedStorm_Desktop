using System;
using System.Collections.Generic;
using System.Text;

namespace PSSApplication.Core
{
    /// <summary>
    /// Object to send to monitor
    /// </summary>
    public struct BLEMeasurement
    {
        public BLEMeasurement(byte pps, byte auc, byte nbv, double [] sc, byte bs)
        {
            // Why is this renamed from pps to pss?
            PSS = pps;
            AUC = auc;
            NBV = nbv;
            SC = sc;
            BS = bs;
        }
        public byte PSS { get; set; }
        public byte AUC { get; set; }

        public byte NBV { get; set; } 
        public double [] SC { get; set; }

        public byte BS { get; set; }
    }
}
