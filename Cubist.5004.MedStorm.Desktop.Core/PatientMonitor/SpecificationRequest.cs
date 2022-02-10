namespace PSSApplication.Core.PatientMonitor
{
    public class SpecificationRequest : IIvoiRequest
    {
        private DeviceSpecification deviceSpecification;

        public SpecificationRequest(DeviceSpecification deviceSpecification)
        {
            this.deviceSpecification = deviceSpecification;
        }

        public IResponseTelegram Process()
        {
            return new SpecificationResponse(deviceSpecification.ReadBinary());
        }
    }
}
