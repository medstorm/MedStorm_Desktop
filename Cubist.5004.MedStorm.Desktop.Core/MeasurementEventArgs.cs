using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace PSSApplication.Core
{
    public struct MeasurementEventArgs //: EventArgs
    {
        static long lastTimestamp = 0;
        public MeasurementEventArgs(byte ppsValue, byte areaValue, byte nerveBlockValue, double[] conductivityItems, byte badSignalValue, float meanRiseTimeValue)
        {
            Measurement = new BLEMeasurement(ppsValue, areaValue, nerveBlockValue, conductivityItems, badSignalValue);
            string condItemsString = string.Join(",", conductivityItems.Select(f => f.ToString(CultureInfo.InvariantCulture.NumberFormat)));
            long timestamp = (long)(DateTime.UtcNow - DateTime.UnixEpoch.ToUniversalTime()).TotalMilliseconds;
            if (timestamp > lastTimestamp + 2000)
                Debug.WriteLine($"Timelag - timestamp={timestamp}, lastTimestamp={lastTimestamp}");

            Message = string.Format("Timestamp:{0}|PPS:{1}|Area:{2}|SkinCond:[{3}]|MeanRiseTime:{4}|NerveBlock:{5}|BadSignal:{6}",
                                            timestamp, ppsValue, areaValue, condItemsString, meanRiseTimeValue.ToString(CultureInfo.InvariantCulture.NumberFormat), nerveBlockValue, badSignalValue);
            lastTimestamp = timestamp;
        }

        public BLEMeasurement Measurement { get; }
        public string Message { get; }
    }
}