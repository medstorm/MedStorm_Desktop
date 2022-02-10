using System;
using System.IO;

namespace PSSApplication.Core.PatientMonitor
{
    public class TelegramReader
    {
        public interface ITelegramTypeReader { ITelegramDataReader ReadTelegramType(); }
        public interface ITelegramDataReader { ITelegramCrcReader ReadData(); }
        public interface ITelegramCrcReader { ITelegramBuilder ReadCrc(); }
        public interface ITelegramBuilder { RequestTelegram MakeTelegram(); }

        public static ITelegramTypeReader MakeReader(BinaryReader reader)
        {
            return new TelegramReaderFluent(reader);
        }

        private class TelegramReaderFluent : ITelegramTypeReader, ITelegramDataReader, ITelegramCrcReader, ITelegramBuilder
        {
            private TelegramByteReader byteReader;
            private RequestTelegramType telegramType;
            private byte[] telegramData;

            public TelegramReaderFluent(BinaryReader reader)
            {
                byteReader = new TelegramByteReader(reader);
            }

            public ITelegramDataReader ReadTelegramType()
            {
                telegramType = (RequestTelegramType)byteReader.Read();
                return this;
            }

            public ITelegramCrcReader ReadData()
            {
                var numberOfDataBytes = byteReader.Read();
                telegramData = byteReader.Read(numberOfDataBytes);
                return this;
            }

            public ITelegramBuilder ReadCrc()
            {
                byteReader.Read(2);
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
                    return new RequestTelegram(telegramType, telegramData);
            }

            private bool TelegramValid()
            {
                return TelegramTypeValid() && byteReader.CrcOk();
            }

            private bool TelegramTypeValid()
            {
                return Enum.IsDefined(typeof(RequestTelegramType), telegramType);
            }
        }
    }
}
