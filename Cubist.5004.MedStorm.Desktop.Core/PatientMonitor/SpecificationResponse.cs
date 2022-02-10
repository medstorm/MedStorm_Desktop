namespace PSSApplication.Core.PatientMonitor
{
    public class SpecificationResponse : IResponseTelegram
    {

        private byte[] data;
        public SpecificationResponse(byte[] data)
        {
            this.data = data;
        }

        public byte[] ToByteArray()
        {
            return data;
        }
    }
}
