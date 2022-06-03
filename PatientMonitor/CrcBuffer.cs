using System.Collections.Generic;

namespace PSSApplication.Core.PatientMonitor
{
    public class CrcBuffer
    {
        private List<byte> contents = new List<byte>();
        public byte[] Contents { get => contents.ToArray(); }

        public void Add(byte value)
        {
            contents.Add(value);
        }

        public void AddRange(byte[] data)
        {
            contents.AddRange(data);
        }

        public void AddCrc()
        {
            var crc16 = CalculateCrc16();
            contents.Add((byte)(crc16 >> 8));
            contents.Add((byte)(crc16 & 0xFF));
        }

        public bool CrcOk()
        {
            return CalculateCrc16() == 0;
        }

        private ushort CalculateCrc16()
        {
            var crcCalculator = new CrcCalculator();
            return crcCalculator.fast_crc16(0, contents.ToArray(), (ushort)(contents.Count));
        }
    }
}
