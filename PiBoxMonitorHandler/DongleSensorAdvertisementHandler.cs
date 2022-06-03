using PSSApplication.Common;
using PSSApplication.Core;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PiBoxMonitorHandler
{
    public class DongleSensorAdvertisementHandler : IPainSensorAdvertisementHandler
    {
        public SerialPort? _donglePort = null;

        public DongleSensorAdvertisementHandler()
        {
        }
        public BLEMeasurement LatestMeasurement { get; private set; } = new BLEMeasurement(0, 0, 0, new double[5], 0);
        public void StartScanningForPainSensors()
        {
            string donglePortName= "/dev/ttyACM0";
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (isWindows)
            {
                donglePortName = "COM7";
            }
            while (true)
            {
                try
                {
                    if (_donglePort == null)
                        _donglePort = new SerialPort(donglePortName, 115200) { DtrEnable = true };

                    if  (!_donglePort.IsOpen)
                        _donglePort.Open();

                    if (_donglePort.IsOpen)
                        HandleSensorDataPackets();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Not able to open={_donglePort}, {ex.Message}");
                    Thread.Sleep(2000); // Milliseconds
                }
            }
        }

        private void HandleSensorDataPackets()
        {
            string message = _donglePort?.ReadLine();
            Console.WriteLine(message);
            byte[] bArray = new byte[100];
            MeasurementEventArgs measurementsArgs = MeasurementEventArgs.ExtractMeasurmentsEvent(bArray);
            LatestMeasurement = measurementsArgs.Measurement;
        }

        public void StopScanningForPainSensors()
        {

        }
    }
}
