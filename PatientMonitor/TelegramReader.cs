using System;
using System.IO;

namespace PSSApplication.Core.PatientMonitor
{
    public class TelegramReader
    {
        private TelegramByteReader _telegramByteReader;
        private RequestTelegramType _requestTelegramType;
        private byte[] _telegramData;
        private CancellationToken _cancellationToken;

        public TelegramReader(BinaryReader reader, CancellationToken token)
        {
            _telegramByteReader = new TelegramByteReader(reader,token);
        }

        public TelegramReader ReadTelegramType()
        {
            // First byte in a request telegram is the telegram type
            _requestTelegramType = (RequestTelegramType)_telegramByteReader.Read();
            return this;
        }

        public TelegramReader ReadData()
        {
            var numberOfDataBytes = _telegramByteReader.Read();
            _telegramData = _telegramByteReader.Read(numberOfDataBytes);
            return this;
        }

        public TelegramReader ReadCrc()
        {
            _telegramByteReader.Read(2);
            return this;
        }

        public RequestTelegram MakeTelegram()
        {
            if (!TelegramValid())
            {
                System.Console.WriteLine("Invalid telegram received");
                throw new IvoiProtocolException("Invalid telegram received");
            }
            else
                return new RequestTelegram(_requestTelegramType, _telegramData);
        }

        private bool TelegramValid()
        {
            return TelegramTypeValid() && _telegramByteReader.CrcOk();
        }

        private bool TelegramTypeValid()
        {
            return Enum.IsDefined(typeof(RequestTelegramType), _requestTelegramType);
        }
    }
}
