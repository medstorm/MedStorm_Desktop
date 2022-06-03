using Serilog;
using System;
using System.IO;

namespace PSSApplication.Core.PatientMonitor
{
    public class DeviceSpecification
    {
        // Specification report File is assumed to be in current directory
        private readonly string _specificationReportFile = Path.GetFullPath("MedStorm.rpt");
        private readonly string _specificationTxtFile = Path.GetFullPath("MedStorm.txt");

        public DeviceSpecification()
        {
        }

        public int ReadNumberOfDecimals()
        {
            try
            {
                using var reader = new StreamReader(_specificationTxtFile);
                var specFile = new SpecificationFile(reader);
                return -specFile.ReadExponent();
            }
            catch (FileNotFoundException e)
            {
                throw new IvoiProtocolException(e.Message);
            }
        }

        public byte[] ReadDeviceIdentifier()
        {
            Log.Debug($"Reading device identifier from {_specificationReportFile}");
            try
            {
                using var reader = new StreamReader(_specificationReportFile);
                var specReport = new SpecificationReport(reader);
                return specReport.ReadDeviceIdentifier();
            }
            catch (FileNotFoundException e)
            {
                throw new IvoiProtocolException($"Could not read device identfier from {_specificationReportFile} \n {e.Message}");
            }
        }
    }
}
