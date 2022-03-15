using Serilog;
using System;
using System.IO;
using System.IO.Ports;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;

namespace PSSApplication.Core.PatientMonitor
{
    internal class ProcessConnection
    {

        public static ConnectionOptions ProcessConnectionOptions()

        {
            ConnectionOptions options = new ConnectionOptions();
            options.Impersonation = ImpersonationLevel.Impersonate;
            options.Authentication = AuthenticationLevel.Default;
            options.EnablePrivileges = true;
            return options;
        }

        public static ManagementScope ConnectionScope(string machineName, ConnectionOptions options, string path)
        {
            ManagementScope connectScope = new ManagementScope();
            connectScope.Path = new ManagementPath(@"\\" + machineName + path);
            connectScope.Options = options;
            connectScope.Connect();
            return connectScope;
        }
    }
    public class MonitorServerRunner
    {
        public string ComPortName { get; private set; }
        public SerialPort port;
        
        private SerialPort OpenSerialPort()
        {
            if (!GetPainMonitorComPort())
                return null;
            var result = new SerialPort
            {
                PortName = ComPortName,
                BaudRate = 19200,
                Handshake = Handshake.None,
                ReadTimeout=100
            };
            result.Open();
            
            return result;
        }
        /// <summary>
        /// This function get the COM port name by the description
        /// Check the description in device manager
        /// </summary>
        /// <returns></returns>
        private bool GetPainMonitorComPort()
        {
            var options = ProcessConnection.ProcessConnectionOptions();
            var connectionScope = ProcessConnection.ConnectionScope(Environment.MachineName, options, @"\root\CIMV2");
            var objectQuery = new ObjectQuery("SELECT * FROM Win32_PnPEntity WHERE ConfigManagerErrorCode = 0");
            var comPortSearcher = new ManagementObjectSearcher(connectionScope, objectQuery);
            using (comPortSearcher)
            {
                Log.Information("Searching for available COM port");
                var deviceObjects = comPortSearcher.Get();
                if (deviceObjects.Count == 0)
                {
                    Log.Warning("Unable to load any devices, cannot search for monitor COM port");
                    return false;
                }
                foreach (var o in deviceObjects)
                {
                    var obj = (ManagementObject) o;
                    var captionObj = obj?["Caption"];
                    if (captionObj == null) continue;

                    var caption = captionObj.ToString();
                    if (!caption.Contains("(COM")) continue;

                    var name = caption.Substring(caption.LastIndexOf("(COM")).Replace("(", string.Empty).Replace(")", string.Empty);
                    Log.Information($"Localized COM port with name: {name} and description \n{caption}");
                    if (caption.Contains("Prolific USB-to-Serial Comm Port") || caption.Contains("USB Serial Port (COM4)"))
                    { 
                        ComPortName = name;
                        return true;
                    }
                }
            }
            Log.Error("Located devices, but did not find Philips monitor COM port. Is the USB cable connected?");
            return false;
        }
        public MonitorServerRunner(MonitorServer server, CancellationToken token)
        {
            Console.WriteLine("Attempting to open serial port...");
            port = OpenSerialPort();
            Console.WriteLine($"Port {port}");
            if (port == null)
                throw new System.ArgumentException("Serial port parameter cannot be null", "port");
            
            var reader = new BinaryReader(port.BaseStream);
            var writer = new BinaryWriter(port.BaseStream);

            //TODO, handle failure to open COM port and "InvalidOperationException" from server
            //TODO, handle reconfiguration of COM port

            var result = Task.Run(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    server.ProcessRequest(reader, writer);
                }
            },
            token);
        }
        public void ClosePort()
        {
            port.Close();
        }
    }
}
