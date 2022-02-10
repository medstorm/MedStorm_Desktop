using System;
using System.Globalization;
using System.Linq;

namespace PSSApplication.Core
{
    public struct MeasurementEventArgs //: EventArgs
    {
        public MeasurementEventArgs(byte ppsValue, byte areaValue, byte nerveBlockValue, double[] conductivityItems, byte badSignalValue, float meanRiseTimeValue)
        {
            Measurement = new BLEMeasurement(ppsValue, areaValue, nerveBlockValue, conductivityItems, badSignalValue);
            string condItemsString = string.Join(",", conductivityItems.Select(f => f.ToString(CultureInfo.CreateSpecificCulture("en-US"))));
            long timestamp = (long)(DateTime.UtcNow - DateTime.UnixEpoch.ToUniversalTime()).TotalMilliseconds;
            Message = string.Format("Timestamp:{0}|PPS:{1}|Area:{2}|SkinCond:[{3}]|MeanRiseTime:{4}|NerveBlock:{5}|BadSignal:{6}",
                                            timestamp, ppsValue, areaValue, condItemsString, meanRiseTimeValue.ToString(CultureInfo.GetCultureInfo("us-US").NumberFormat), nerveBlockValue, badSignalValue);
        }

        public BLEMeasurement Measurement { get; }
        public string Message { get; }
    }
}