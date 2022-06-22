using Serilog;
using System;
using System.IO;
using System.IO.Ports;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PSSApplication.Core.PatientMonitor
{
    public class PatientMonitorSerialPort
    {
        public static string ComPortName { get; private set; }
        public static SerialPort? _port = null;
        private static PatientMonitorSerialPort? _instance = null;

        /// <summary>
        /// This function get the COM port name by the description
        /// Check the description in device manager
        /// </summary>
        /// <returns></returns>
        private static bool GetPainMonitorComPort()
        {



            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (isWindows)
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                "root\\CIMV2", "SELECT * FROM Win32_PnPEntity WHERE ClassGuid=\"{4d36e978-e325-11ce-bfc1-08002be10318}\"");

                List<string> portList = new List<string>();
                foreach (ManagementObject item in searcher.Get())
                {
                    portList.Add(item["Caption"].ToString());
                }

                Log.Information("Searching for available COM port");
                //string[] ports = SerialPort.GetPortNames();
                if (portList.Count == 0)
                {
                    Log.Warning("Unable to find any COM ports");
                    return false;
                }
                foreach (var portName in portList)
                {
                    if (!portName.Contains("COM"))
                        continue;

                    if (portName.Contains("Prolific USB-to-Serial Comm Port"))
                    {
                        string[] portNameSplit= portName.Split(new char[] { '(',')' });
                        ComPortName = portNameSplit[1];

                        return true;
                    }
                }

                Log.Error("Did not find Philips monitor COM port. Is the USB cable connected?");
                return false;
            }
            else // Linux
            {
                ComPortName = "/dev/ttyUSB0";
                return true;
            }
        }

        public static void OpenSerialPort(MonitorServer server, CancellationToken token)
        {
            Log.Debug("Attempting to open serial port...");
            if (!GetPainMonitorComPort())
            {
                _port = null;
                return;
            }

            Log.Information($"Found port to Philips Monitor: {ComPortName}");
            _port = new SerialPort
            {
                PortName = ComPortName,
                BaudRate = 19200,
                Handshake = Handshake.None,
                ReadTimeout = 100,
                DtrEnable = true,
                RtsEnable = true,
            };

            _port.Open();

            var reader = new BinaryReader(_port.BaseStream);
            var writer = new BinaryWriter(_port.BaseStream);

            var result = Task.Run(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    server.ProcessRequest(reader, writer, token);
                }
            },
            token);
        }

        public static void ClosePort()
        {
            _port?.Close();
        }

        public static int ReadByte()
        {
            if (_port == null)
                throw new InvalidOperationException("ReadByte() - No port open");
            return _port.ReadByte();
        }
    }
}
