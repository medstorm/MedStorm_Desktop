using PSSApplication.Common;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PSSApplication.Common
{
    public class DongleSensorAdvertisementHandler : IPainSensorAdvertisementHandler
    {
        public static IPainSensorAdvertisementHandler m_advertisementHandlerSingleton = null;

        public event EventHandler<MeasurementEventArgs> NewMeasurement;
        public SerialPort? m_donglePort = null;
        Thread? m_dataFromDongleThread;
        bool m_useThread;

        public DongleSensorAdvertisementHandler(bool useThread = false)
        {
            m_useThread = useThread;
        }

        public static IPainSensorAdvertisementHandler CreateAdvertisementHandler()
        {
            if (m_advertisementHandlerSingleton == null)
                m_advertisementHandlerSingleton = new DongleSensorAdvertisementHandler(useThread: true);

            return m_advertisementHandlerSingleton;
        }
        public BLEMeasurement LatestMeasurement { get; private set; } = new BLEMeasurement(0, 0, 0, new double[5], 0);
        public void StartScanningForPainSensors()
        {
            string donglePortName = "/dev/ttyACM0";
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (isWindows)
            {
                donglePortName = "COM7";
            }
            while (true)
            {
                try
                {
                    if (m_donglePort == null)
                        m_donglePort = new SerialPort(donglePortName, 115200) { DtrEnable = true };

                    if (!m_donglePort.IsOpen)
                        m_donglePort.Open();

                    if (m_donglePort.IsOpen)
                    {
                        if (m_useThread)
                        {
                            m_dataFromDongleThread = new Thread(new ThreadStart(HandleSensorDataPacketsWithThread));
                            m_dataFromDongleThread.Start();
                            return;
                        }
                        else
                            HandleSensorDataPackets();
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Not able to open={m_donglePort}, {ex.Message}");
                    Thread.Sleep(2000); // Milliseconds
                }
            }
        }

        private void HandleSensorDataPacketsWithThread()
        {
            bool ok = true;
            while (ok)
            {
                ok= HandleSensorDataPackets();
            }
        }

        private bool HandleSensorDataPackets()
        {
            try
            {
                string message = m_donglePort?.ReadLine();
                if (string.IsNullOrEmpty(message))
                {
                    Thread.Sleep(1000); // milli second
                    return false;
                }

                Console.WriteLine(message);
                string[] stringValues= message.Split(',');
                byte[] bArray = new byte[100];
                for (int i = 0; i < 28; i++)
                {
                    bArray[i]= byte.Parse(stringValues[i].Trim(), NumberStyles.HexNumber);
                    //bArray[i]= Convert.ToByte(stringValues[i], style);
                }

                MeasurementEventArgs measurementsArgs = MeasurementEventArgs.ExtractMeasurmentsEvent(bArray);
                LatestMeasurement = measurementsArgs.Measurement;
                if (NewMeasurement != null)
                    NewMeasurement(this, measurementsArgs);
            }
            catch (Exception e)
            {
                Log.Error($"HandleSensorDataPackets(): Error={e.Message}");
                Thread.Sleep(1000); // milli second
                return false;
            }
            return true;
        }

        public void StopScanningForPainSensors()
        {
            if (m_useThread && m_dataFromDongleThread != null)
            {
                ClosePort();

                m_dataFromDongleThread = null;
            }
        }

        private void ClosePort()
        {
            if (m_donglePort != null)
            {
                m_donglePort.Close();
                m_donglePort = null;
            }
        }

        public void Close()
        {
            ClosePort();
        }
    }
}
