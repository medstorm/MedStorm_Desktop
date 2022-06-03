using PSSApplication.Common;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;


namespace PSSApplication.Core.PatientMonitor
{
    public delegate byte[] SpecificationRequestDelegate(string sender);
    public delegate BLEMeasurement OperatingDataRequestDelegate();

    public class MonitorServer
    {
        public event OperatingDataRequestDelegate OperatingDataRequest;

        private readonly Dictionary<RequestTelegramType, IIvoiRequest> telegramHandlers;
        DeviceSpecification _deviceSpecification = new DeviceSpecification();
        public MonitorServer()
        {
            _deviceSpecification = new DeviceSpecification();
        }

        public void ProcessRequest(BinaryReader fromMonitor, BinaryWriter toMonitor, CancellationToken token)
        {
            try
            {
                RequestTelegram requestTelegram = ReadTelegram(fromMonitor,token);
                IIvoiRequest request;
                if (requestTelegram.TelegramType == RequestTelegramType.SpecificationRequest)
                    request = new SpecificationRequest();
                else
                    request = new OperatingDataRequest(_deviceSpecification, OnOperatingDataRequest);

                var responseTelegram = request.Process(token);
                if (responseTelegram != null)
                {
                    toMonitor.Write(responseTelegram.ToByteArray());
                    Log.Debug($"OperatingDataResponse sent to monitor sent after {requestTelegram.TelegramType}");
                }
            }
            catch (IvoiProtocolException ex)
            {
                Log.Error($"MonitorServer.ProcessRequest() got IvoiProtocolException= {ex.Message}");
            }
        }

        private BLEMeasurement OnOperatingDataRequest()
        {
            var result = OperatingDataRequest?.Invoke();
            if (result == null)
                throw new IvoiProtocolException("OperatingDataRequest didn't get valid BLEMeasurement");
            else
                return (BLEMeasurement)result;
        }

        private RequestTelegram ReadTelegram(BinaryReader fromMonitor, CancellationToken token)
        {
            try
            {
                return RequestTelegram.Read(fromMonitor,token);
            }
            catch (TimeoutException ex)
            {
                Log.Error($"ReadTelegram got TimeoutException= {ex.Message}");
                throw new IvoiProtocolException();
            }
        }
    }
}
