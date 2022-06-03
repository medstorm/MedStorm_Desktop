namespace PSSApplication.Core.PatientMonitor
{
    public class SpecificationResponse : ResponseTelegram
    {
        public SpecificationResponse(byte[] data)
            : base(data)
        {
        }

        override public byte[] ToByteArray()
        {
            return data;
        }
    }
}
