﻿namespace PSSApplication.Core.PatientMonitor
{
    public class OperatingDataResponse : IResponseTelegram
    {

        private byte[] data;
        public OperatingDataResponse(byte[] data)
        {
            this.data = data;
        }

        private byte[] NumberOfDataBytes()
        {
            var numberOfDataBytes = data.Length;
            if (numberOfDataBytes <= 254)
            {
                return new byte[] { (byte)numberOfDataBytes };
            }
            else
            {
                return new byte[] {
                    0xFF,
                    (byte)(numberOfDataBytes >> 8),
                    (byte)(numberOfDataBytes & 0xFF)
                };
            }
        }
        public byte[] ToByteArray()
        {
            var buffer = new CrcBuffer();
            buffer.Add((byte)ResponseTelegramType.OperatingDataResponse);
            buffer.AddRange(NumberOfDataBytes());
            buffer.AddRange(data);
            buffer.AddCrc();
            return buffer.Contents;
        }
    }
}
