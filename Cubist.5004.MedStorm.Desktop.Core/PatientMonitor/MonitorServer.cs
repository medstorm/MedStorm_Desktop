using System;
using System.Collections.Generic;
using System.IO;
using Windows.Devices.Sensors;


namespace PSSApplication.Core.PatientMonitor
{
    public delegate byte[] SpecificationRequestDelegate(string sender);
    public delegate BLEMeasurement OperatingDataRequestDelegate();

    public class MonitorServer
    {
        private readonly Dictionary<RequestTelegramType, IIvoiRequest> telegramHandlers;
        
        public event OperatingDataRequestDelegate OperatingDataRequest;

        public MonitorServer(string specificationTextFile)
        {
            var deviceSpecification = new DeviceSpecification(specificationTextFile);
            telegramHandlers = new Dictionary<RequestTelegramType, IIvoiRequest> {
                {
                    RequestTelegramType.SpecificationRequest,
                    new SpecificationRequest(deviceSpecification)
                },
                {
                    RequestTelegramType.OperatingDataRequest,
                    new OperatingDataRequest(deviceSpecification, OnOperatingDataRequest)
                }
            };
        }

        public void ProcessRequest(BinaryReader fromMonitor, BinaryWriter toMonitor)
        {
            try
            {
                var requestTelegram = ReadTelegram(fromMonitor);
                var request = telegramHandlers[requestTelegram.TelegramType];
                var responseTelegram = request.Process();
                if (responseTelegram != null)
                {
                    toMonitor.Write(responseTelegram.ToByteArray());
                }
            }
            catch (IvoiProtocolException)
            {
            }
        }

        private BLEMeasurement OnOperatingDataRequest()
        {
            var result = OperatingDataRequest?.Invoke();
            if (result == null)
                throw new IvoiProtocolException();
            else
                return (BLEMeasurement)result;
        }

        private RequestTelegram ReadTelegram(BinaryReader fromMonitor)
        {
            try
            {

                return RequestTelegram.Read(fromMonitor);
            }
            catch (TimeoutException)
            {
                throw new IvoiProtocolException();
            }
        }
    }
}
