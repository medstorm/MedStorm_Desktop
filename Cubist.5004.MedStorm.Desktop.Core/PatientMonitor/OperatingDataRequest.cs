using System;
using System.Collections.Generic;

namespace PSSApplication.Core.PatientMonitor
{
    public class OperatingDataRequest : IIvoiRequest
    {
        private DeviceSpecification deviceSpecification;
        private OperatingDataRequestDelegate OnOperatingDataRequest;

        public OperatingDataRequest(
            DeviceSpecification deviceSpecification,
            OperatingDataRequestDelegate onOperatingDataRequest)
        {
            this.deviceSpecification = deviceSpecification;
            OnOperatingDataRequest = onOperatingDataRequest;
        }

        public IResponseTelegram Process()
        {
            var operatingDataResponse = OnOperatingDataRequest();
            //if (operatingDataResponse == null)
            //{
            //    return null;
            //}

            // Why is this converted to float?
            float nbv = operatingDataResponse.NBV;
            var telegramData = new List<byte>();
            telegramData.Add(0x00); //alarm state
            telegramData.Add(0x00); //general inop indication
            telegramData.Add(0x00); //unspecific inop indication
            telegramData.Add(0x00); //unspecific alarm indication
            telegramData.AddRange(deviceSpecification.ReadDeviceIdentifier());
            telegramData.AddRange(NumericRecord(operatingDataResponse.PSS));
            telegramData.AddRange(NumericRecord(operatingDataResponse.AUC));
            telegramData.AddRange(NumericRecord(nbv));
            telegramData.AddRange(NumericRecord(operatingDataResponse.BS));

            return new OperatingDataResponse(telegramData.ToArray());
        }

        private byte[] NumericRecord(double value)
        {
            var mantissa = Mantissa(value);
            return new byte[]
            {
                (byte)(mantissa >> 8),
                (byte)(mantissa & 0xFF)
            };
        }

        private ushort Mantissa(double value)
        {
            return (ushort)Math.Round(value * Math.Pow(10, deviceSpecification.ReadNumberOfDecimals()));
        }
    }
}