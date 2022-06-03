using Serilog;

namespace PSSApplication.Core.PatientMonitor
{
    public class SpecificationRequest : IIvoiRequest
    {
        public SpecificationRequest()
        {
        }

        public ResponseTelegram? Process(CancellationToken token)
        {
            string _specificationBinaryFile = Path.GetFullPath("MedStorm.bin");
            Log.Debug($"Reading identification from {_specificationBinaryFile}");
            try
            {
                return new SpecificationResponse(File.ReadAllBytes(_specificationBinaryFile));
            }
            catch (Exception ex)
            {
                Log.Error($"Unable to read specification binary file {_specificationBinaryFile}, {ex.Message}");
                return null;
            }

        }
    }
}
