using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using PSSApplication.Common;

namespace PSSApplication.Core.PatientMonitor
{
    public class MonitorHandler
    {
        private MonitorServer _monitorServer;
        CancellationTokenSource _source;
        CancellationToken _token;
        private readonly IPainSensorAdvertisementHandler _painSensorAdvertisementHandler;

        public MonitorHandler(IPainSensorAdvertisementHandler painSensorAdvertisementHandler)
        {
            _painSensorAdvertisementHandler = painSensorAdvertisementHandler;
        }

        public bool ConnectToMonitor()
        {
            _monitorServer = new MonitorServer();
            _monitorServer.OperatingDataRequest += GetPainData; //Analysis.GetPainAreaInView;
            _source = new CancellationTokenSource();
            _token = _source.Token;

            try
            {
                // if exception thrown pop-up display and ask user to select right monitorComPort from setting.
                // On selection, save monitorComPort and restart MonitorServerRunner
                PatientMonitorSerialPort.OpenSerialPort(_monitorServer, _token);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to connect to monitor, Error={ex.Message}");
                Log.Error($"Unable to connect to monitor, Error={ex.Message}");
                return false;
            }
        }

        public async void DisconnectMonitor()
        {
            if (_source != null)
            {
                _source.Cancel();
                await Task.Delay(1000);
                PatientMonitorSerialPort.ClosePort();
            }
        }

        private BLEMeasurement GetPainData()
        {
            var paindata = _painSensorAdvertisementHandler.LatestMeasurement;
            return paindata;
        }
    }
}
