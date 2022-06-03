using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PSSApplication.Core.PatientMonitor
{
    public class SpecificationReport
    {
        private TextReader reportReader;

        public SpecificationReport(TextReader reportReader)
        {
            this.reportReader = reportReader;
        }

        public byte[] ReadDeviceIdentifier()
        {
            var result = new List<byte>();
            while (true)
            {
                var line = reportReader.ReadLine();
                if (line == null)
                    return DeviceIdentifier(result);
                else if (IsDeviceIdentifierByte(line))
                    result.Add(DeviceIdentifierByte(line));
            }
        }

        private bool IsDeviceIdentifierByte(string line)
        {
            return line.Contains("device identification");
        }

        private byte DeviceIdentifierByte(string line)
        {
            var i = line.IndexOf("0x");
            var idByte = line.Skip(i + 2).Take(2);
            return byte.Parse(string.Concat(idByte), NumberStyles.HexNumber);
        }

        private byte[] DeviceIdentifier(List<byte> deviceIdentifier)
        {
            if (deviceIdentifier.Count != 4)
                throw new IvoiProtocolException("Invalid device identifier");
            else
                return deviceIdentifier.ToArray();
        }
    }
}
