using System;
using System.Collections.Generic;
using System.Text;

namespace PSSApplication.Common
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
        public byte PSS { get; set; }   // PPS value
        public byte AUC { get; set; }   // Area Value

        public byte NBV { get; set; }   // Nerve-Block value
        public double [] SC { get; set; }   // Skin Conductivity 

        public byte BS { get; set; }    // Bad Signal value
    }
}
