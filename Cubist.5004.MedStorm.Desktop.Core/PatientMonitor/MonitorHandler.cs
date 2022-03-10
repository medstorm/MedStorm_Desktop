using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PSSApplication.Core.PatientMonitor
{
   public class MonitorHandler
   {
        private MonitorServer monitorServer;
        private MonitorServerRunner monitorRunner;

        CancellationTokenSource source;
        CancellationToken token;

        private readonly IConfiguration _configuration;

        public MonitorHandler(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public bool ConnectToMonitor()
        {
            var specToolSettings = _configuration.GetSection("SpecToolPath");
            var useAbsolutePath = Convert.ToBoolean(specToolSettings.GetSection("UseAbsolutePath").Value);

            // Path to monitor specification
            // Use relative path when generating the installer
            var pathWithSpecTool = useAbsolutePath ?
                specToolSettings.GetSection("AbsolutePath").Value :
                Path.Join(Directory.GetCurrentDirectory(), "PatientMonitor");
            
            var specificationPath = Path.Join(pathWithSpecTool, "SpecTool", "MedStorm.txt");
            Console.WriteLine( $"Using path {specificationPath} for monitor specification");

            monitorServer = new MonitorServer(specificationPath);
            monitorServer.OperatingDataRequest += GetPainData; //Analysis.GetPainAreaInView;
            source = new CancellationTokenSource();
            token = source.Token;
            
            try
            {
                monitorRunner = new MonitorServerRunner(monitorServer, token);  // if exception thrown pop-up display and ask user to select right monitorComPort from setting.
                                                                                // On selection, save monitorComPort and restart MonitorServerRunner

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to connect to monitor, Error={ex.Message}");
                monitorRunner = null;
                return false;
            }


        }

        public async void DisconnectMonitor()
        {
            if (source != null)
            {
                source.Cancel();
                await Task.Delay(1000);
                monitorRunner?.ClosePort();
            }
        }

        private BLEMeasurement GetPainData()
        {
            var paindata = BleEndpoint.LatestMeasurement;
            return paindata;
        }
    }
}
