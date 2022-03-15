using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using PSSApplication.Core.PatientMonitor;
using Serilog;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PSSApplication.Core
{
    public class BleEndpoint : Hub
    {
        public static bool m_pageReloaded;
        public static MonitorHandler m_monitor;

        private MockData mock;
        private BleHub m_bleHub;

        private readonly IConfiguration m_configuration;
        private IHubContext<BleEndpoint> m_context=null;
        private static bool m_firstCallToConstructor = true;

        public BleEndpoint()
        {

        }
        public BleEndpoint(IHubContext<BleEndpoint> context)
        {
            Log.Debug($"BleEndpoint: ctor, m_firstCallToConstructor={m_firstCallToConstructor}");
            var builder = new ConfigurationBuilder()
                .AddJsonFile($"appsettings.json", true, true)
                .AddEnvironmentVariables();

            m_context = context;
            m_configuration = builder.Build();

            var debug = m_configuration.GetValue<string>("Debug").ToLower() == "true";
            if (m_context != null && m_context.Clients != null)
            {
                m_context.Clients.All.SendAsync("CheckIfDebug", debug);
            }

            var isMock = (m_configuration.GetValue<string>("Mock").ToLower() == "true");
            if (isMock)
            {
                mock = new MockData();
                mock.NewMeasurement += AddMeasurement;
            }
            else
            {
                m_bleHub = new BleHub(context, m_configuration.GetValue<string>("AdvertisingName"));
                if (m_firstCallToConstructor)
                {
                    Log.Debug("BleEndpoint.ctor - Adding AddMeasurement");
                    m_firstCallToConstructor = false;
                    AdvertisementHandler.AdvertisementMgr.NewMeasurement += AddMeasurement;
                }
            }
        }

        public static BLEMeasurement LatestMeasurement { get; private set; } = new BLEMeasurement(0, 0, 0, new double[5], 0);

        //public Task StartListningForPainSensors()
        //{
        //    Log.Debug("BleEndpoint: StartListningForPainSensors");
        //    DataExporter.CreateExcelFile();

        //    mock?.StartListningForPainSensors();
        //    m_bleHub?.StartScanningForPainSensors();

        //    return Task.CompletedTask;
        //}

        public void StopScanningForPainSensors()
        {
            Log.Debug("BleEndpoint: StopScanningForPainSensors");
            mock?.StopListningForPainSensors();
            if (m_bleHub != null)
            {
                m_bleHub.StopScanningForPainSensors();
            }
        }

        public Task PageReloaded()
        {
            Log.Debug("BleEndpoint-PageReloaded");
            BleEndpoint.m_pageReloaded = true;
            return Task.CompletedTask;
        }

        public async Task AskServerToClose()
        {
            DebugWrite("BleEndpoint.AskServerToClose");
            await Task.Delay(2000);
            if (!BleEndpoint.m_pageReloaded)
            {
                await CloseApplication();
            }
            else
            {
                await SendConnectionStatus();
            }

            BleEndpoint.m_pageReloaded = false;
        }

        public async Task CloseApplication()
        {
            DebugWrite("BleEndpoint.CloseApplication");
            if (m_context != null && m_context.Clients != null)
            {
                await m_context.Clients.All.SendAsync("ClosingApplication");
            }
            mock?.CloseApplication();
            if (m_bleHub != null)
            {
                m_bleHub.CloseApplication();
            }

            DataExporter.DeleteTempFile();

            Environment.Exit(0);
        }

        public async Task ConnectToMonitor()
        {
            DebugWrite("BleEndpoint: Connecting to monitor");
            m_monitor = new MonitorHandler(m_configuration);
            var connectionSuccessful = m_monitor.ConnectToMonitor();

            if (m_context != null && m_context.Clients != null)
                await m_context.Clients.All.SendAsync("MonitorConnectionResult", connectionSuccessful);

        }
        public void DisconnectMonitor()
        {
            DebugWrite("BleEndpoint: Disconnecting monitor");
            m_monitor.DisconnectMonitor();
        }

        public static void DebugWrite(string str, bool onlyDebug = false)
        {
            Log.Debug(str);
            if (!onlyDebug)
                Console.WriteLine(str);
        }

        private async Task SendConnectionStatus()
        {
            DebugWrite("BleEndpoint: SendConnectionStatus");
            bool isPaired;
            if (mock != null)
                isPaired = await mock.IsPaired();
            else
                isPaired = AdvertisementHandler.AdvertisementMgr.IsPaired();

            //bool isPaired = mock?.IsPaired() ?? AdvertisementHandler.AdvertisementMgr.IsPaired();

            if (m_context != null && m_context.Clients != null)
            {
                await m_context.Clients.All.SendAsync("SendConnectionStatus", isPaired);
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

                if (m_context != null && m_context.Clients != null)
                {
                    await m_context.Clients.All.SendAsync("SendMessage", $"combined {e.Message}");
                }

                DataExportObject dataExportObject = new DataExportObject(DateTime.Now.ToString(), e.Measurement.PSS, e.Measurement.AUC, e.Measurement.NBV, e.Measurement.BS, e.Measurement.SC);
                DataExporter.AddData(dataExportObject);
            }
        }

        public bool IsAcceptedRange(BLEMeasurement measurement)
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
                Log.Debug("Accepted range failure. All values are not within accepted range.");
                return false;
            }

            foreach (double i in sc)
            {
                if (i > scMax || i < 0)
                {
                    Log.Debug("Accepted range failure. All values are not within accepted range.");
                    return false;
                }
            }

            return true;
        }
    }
}
