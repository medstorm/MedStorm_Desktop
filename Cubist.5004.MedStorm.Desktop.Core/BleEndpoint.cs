using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using PSSApplication.Core.PatientMonitor;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PSSApplication.Core
{
    public class BleEndpoint : Hub
    {
        private static class Globals
        {
            public static bool PageReloaded;
            public static MonitorHandler Monitor;
        }

        private MockData mock;
        private BleAdvertisementWatcher bleAdvertisementWatcher;

        private readonly IConfiguration _configuration;
        private IHubContext<BleEndpoint> _context;

        public BleEndpoint(IHubContext<BleEndpoint> context)
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile($"appsettings.json", true, true)
                .AddEnvironmentVariables();

            _configuration = builder.Build();
            _context = context;

            var debug = _configuration.GetValue<string>("Debug").ToLower() == "true";
            if (_context != null && _context.Clients != null)
            {
                _context.Clients.All.SendAsync("CheckIfDebug", debug);
            }

            var isMock = (_configuration.GetValue<string>("Mock").ToLower() == "true");
            if (isMock)
            {
                mock = new MockData();
                mock.NewMeasurement += AddMeasurement;
            }
            else
            {
                bleAdvertisementWatcher = new BleAdvertisementWatcher(context, _configuration.GetValue<string>("AdvertisingName"));
                bleAdvertisementWatcher.NewMeasurement += AddMeasurement;
            }
        }

        public static BLEMeasurement LatestMeasurement { get; private set; }  = new BLEMeasurement(0, 0, 0, new double[5], 0);

        public Task StartAdvertising()
        {
            DataExporter.CreateExcelFile();

            mock?.StartAdvertising();
            bleAdvertisementWatcher?.StartScanningForPainSensors();

            return Task.CompletedTask;
        }

        public void StopAdvertising()
        {
            mock?.StopAdvertising();
            if (bleAdvertisementWatcher != null)
            {
                bleAdvertisementWatcher.StopAdvertising();
            }
        }

        public Task PageReloaded()
        {
            DebugWrite("PageReloaded");
            Globals.PageReloaded = true;
            return Task.CompletedTask;
        }

        public async Task AskServerToClose()
        {
            DebugWrite("AskServerToClose");
            await Task.Delay(2000);
            if (!Globals.PageReloaded)
            {
                await CloseApplication();
            }
            else
            {
                await SendConnectionStatus();
            }

            Globals.PageReloaded = false;
        }
 
        public async Task CloseApplication()
        {
            DebugWrite("CloseApplication");
            if (_context != null && _context.Clients != null)
            {
                await _context.Clients.All.SendAsync("ClosingApplication");
            }
            mock?.CloseApplication();
            if (bleAdvertisementWatcher != null)
            {
                bleAdvertisementWatcher.CloseApplication();
            }

            DataExporter.DeleteIfNotAlreadyDeleted();

            Environment.Exit(0);
        }

        public async Task ConnectToMonitor()
        {
            DebugWrite("Connecting to monitor");
            Globals.Monitor = new MonitorHandler(_configuration);
            var connectionSuccessful = Globals.Monitor.ConnectToMonitor();

            if (_context != null && _context.Clients != null)
                await _context.Clients.All.SendAsync("MonitorConnectionResult", connectionSuccessful);

        }
        public void DisconnectMonitor()
        {
            DebugWrite("Disconnecting monitor");
            Globals.Monitor.DisconnectMonitor();
        }

        public static void DebugWrite(string str, bool onlyDebug = false)
        {
            Debug.WriteLine(str);
            if (!onlyDebug)
                Console.WriteLine(str);
        }

        private async Task SendConnectionStatus()
        {
            DebugWrite("SendConnectionStatus");
            bool isPaired = mock?.IsPaired() ?? bleAdvertisementWatcher.IsPaired();

            if (_context != null && _context.Clients != null)
            {
                await _context.Clients.All.SendAsync("SendConnectionStatus", isPaired);
            }
        }

        // Using an event for this has the undesirable side effect of making this having async void signature.
        // Perhaps another design should be chosen to avoid this?
        private async void AddMeasurement(object sender, MeasurementEventArgs e)
        {
            LatestMeasurement = e.Measurement;

            if (IsAcceptedRange(e.Measurement) && e.Message != "")
            {
                //DebugWrite($"New measurement   {e.Message}");
                //Debug.Flush();

                if (_context != null && _context.Clients != null)
                {
                    await _context.Clients.All.SendAsync("SendMessage", $"combined {e.Message}");
                }

                DataExportObject dataExportObject = new DataExportObject(DateTime.Now.ToString(), e.Measurement.PSS, e.Measurement.AUC, e.Measurement.NBV, e.Measurement.BS, e.Measurement.SC);
                DataExporter.AddData(dataExportObject);
            }
        }

        private bool IsAcceptedRange(BLEMeasurement measurement)
        {
            const int ppsMax = 10;
            const int aucMax = 100;
            const int nbMax = 10;
            const float scMax = 200.0F;

            int pps = measurement.PSS;
            int auc = measurement.AUC;
            int nb = measurement.NBV;
            double[] sc = measurement.SC;
            int bs = measurement.BS;
            if (pps > ppsMax || pps < 0 || auc > aucMax || auc < 0 || nb > nbMax || nb < 0 || bs > 1 || bs < 0)
            {
                BleEndpoint.DebugWrite("Accepted range failure. All values are not within accepted range.");
                return false;
            }

            foreach (double i in sc)
            {
                if (i > scMax || i < 0)
                {
                    BleEndpoint.DebugWrite("Accepted range failure. All values are not within accepted range.");
                    return false;
                }
            }

            return true;
        }
    }
}
