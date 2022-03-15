using Serilog;
using System;
using System.IO;

namespace PSSApplication.Core.PatientMonitor
{
    public class DeviceSpecification
    {
        private readonly string _specificationTextFile;

        public DeviceSpecification(string specificationTextFile)
        {
            _specificationTextFile = specificationTextFile;
        }

        public byte[] ReadBinary()
        {
            Log.Debug($"Reading specification binary from {Path.ChangeExtension(_specificationTextFile, "bin")}");
            try
            {
                return File.ReadAllBytes(Path.ChangeExtension(_specificationTextFile, "bin"));
            }
            catch (System.IO.DirectoryNotFoundException)
            {
                Log.Error($"Unable to read specification file at path {_specificationTextFile}");
                throw;
            }

        }

        public int ReadNumberOfDecimals()
        {
            try
            {
                using var reader = new StreamReader(_specificationTextFile);
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
            Log.Debug($"Reading device identifier from {Path.ChangeExtension(_specificationTextFile, "rpt")}");
            try
            {
                var reportFile = Path.ChangeExtension(_specificationTextFile, ".rpt");
                using var reader = new StreamReader(reportFile);
                var specReport = new SpecificationReport(reader);
                return specReport.ReadDeviceIdentifier();
            }
            catch (FileNotFoundException e)
            {
                throw new IvoiProtocolException(e.Message);
            }
        }
    }
}
