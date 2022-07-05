using PSSApplication.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSSApplication.Common
{
    public interface IPainSensorAdvertisementHandler
    {
        public BLEMeasurement LatestMeasurement { get; }

        public event EventHandler<MeasurementEventArgs> NewMeasurement;
        public void StartScanningForPainSensors();
        public void StopScanningForPainSensors();
        void Close();
    }
}
