using PSSApplication.Common;
using Serilog;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace PSSApplication.Common
{
    public struct MeasurementEventArgs
    {
        const int NumOfCondItems = 5;
        const int NumBytesFloats = 4;
        static long lastTimestamp = 0;
        public MeasurementEventArgs(byte ppsValue, byte areaValue, byte nerveBlockValue, double[] conductivityItems, byte badSignalValue, float meanRiseTimeValue)
        {
            Measurement = new BLEMeasurement(ppsValue, areaValue, nerveBlockValue, conductivityItems, badSignalValue);
            string condItemsString = string.Join(",", conductivityItems.Select(f => f.ToString(CultureInfo.InvariantCulture.NumberFormat)));
            long timestamp = (long)(DateTime.UtcNow - DateTime.UnixEpoch.ToUniversalTime()).TotalMilliseconds;
            if (lastTimestamp != 0 && timestamp > lastTimestamp + 2000)
                Log.Warning($"MeasurementEventArgs: Timelag - timestamp={timestamp}, lastTimestamp={lastTimestamp}");

            Message = string.Format("Timestamp:{0}|PPS:{1}|Area:{2}|SkinCond:[{3}]|MeanRiseTime:{4}|NerveBlock:{5}|BadSignal:{6}",
                                            timestamp, ppsValue, areaValue, condItemsString, meanRiseTimeValue.ToString(CultureInfo.InvariantCulture.NumberFormat), nerveBlockValue, badSignalValue);
            lastTimestamp = timestamp;
        }

        public BLEMeasurement Measurement { get; }
        public string Message { get; }

        public bool IsAcceptedRange()
        {
            const int ppsMax = 10;
            const int aucMax = 100;
            const int nbMax = 10;
            const float scMax = 200.0F;

            int pps = Measurement.PSS;
            int auc = Measurement.AUC;
            int nb = Measurement.NBV;
            double[] sc = Measurement.SC;
            int bs = Measurement.BS;
            if (pps > ppsMax || pps < 0 || auc > aucMax || auc < 0 || nb > nbMax || nb < 0 || bs > 1 || bs < 0)
            {
                Log.Warning("Accepted range failure. All values are not within accepted range.");
                return false;
            }

            foreach (double i in sc)
            {
                if (i > scMax || i < 0)
                {
                    //Log.Warning("Accepted range failure. All values are not within accepted range.");
                    return false;
                }
            }

            return true;
        }

        public static MeasurementEventArgs ExtractMeasurmentsEvent(byte[] bArray)
        {
            byte ppsValue = bArray[0];

            byte areaValue = bArray[1];
            double[] ConductivityItems = new double[NumOfCondItems];
            for (int i = 0; i < NumOfCondItems; i++)
            {
                byte[] condItemBytes = new byte[4];
                Array.Copy(bArray, 2 + i * NumBytesFloats, condItemBytes, 0, NumBytesFloats);
                double floatToDouble = (double)(new decimal(BitConverter.ToSingle(condItemBytes, 0)));
                ConductivityItems[i] = floatToDouble;
            }

            float meanRiseTimeValue = BitConverter.ToSingle(bArray, 22);
            byte nerveBlockValue = bArray[26];
            byte badSignalValue = bArray[27];

            MeasurementEventArgs measurementsArgs = new MeasurementEventArgs(ppsValue, areaValue, nerveBlockValue, ConductivityItems, badSignalValue, meanRiseTimeValue);
            return measurementsArgs;
        }
    }
}