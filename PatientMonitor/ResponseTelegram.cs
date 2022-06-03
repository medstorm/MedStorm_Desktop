
namespace PSSApplication.Core.PatientMonitor
{
    public abstract class ResponseTelegram
    {
        protected byte[] data;
        protected ResponseTelegram(byte[] data)
        {
            this.data = data;
        }
        public abstract byte[] ToByteArray();
    }
}
