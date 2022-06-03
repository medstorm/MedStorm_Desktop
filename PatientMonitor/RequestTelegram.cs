using System.IO;

namespace PSSApplication.Core.PatientMonitor
{
    public class RequestTelegram
    {
        public RequestTelegramType TelegramType { get; private set; }
        public byte[] Data { get; private set; }

        public RequestTelegram(RequestTelegramType telegramType, byte[] data)
        {
            TelegramType = telegramType;
            Data = data;
        }

        public static RequestTelegram Read(BinaryReader reader, CancellationToken token)
        {
            return new TelegramReader(reader, token)
                .ReadTelegramType()
                .ReadData()
                .ReadCrc()
                .MakeTelegram();
        }
    }
}
