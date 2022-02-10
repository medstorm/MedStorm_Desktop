using System;
using System.Collections.Generic;
using System.IO;

namespace PSSApplication.Core.PatientMonitor
{
    public class TelegramByteReader
    {
        private BinaryReader binaryReader;
        private CrcBuffer buffer = new CrcBuffer();

        public TelegramByteReader(BinaryReader reader)
        {
            binaryReader = reader;
        }

        public byte[] Read(int numberOfBytes)
        {
            var data = new List<byte>();
            for (int i = 0; i < numberOfBytes; i++)
            {
                data.Add(Read());
            }
            return data.ToArray();
        }

        public byte Read()
        {
            try
            {
                var result = binaryReader.ReadByte();
                buffer.Add(result);
                return result;
            }
            catch (UnauthorizedAccessException e)
            {
                throw new InvalidOperationException(e.ToString());
            }
            catch (EndOfStreamException e)
            {
                throw new InvalidOperationException(e.ToString());
            }
            catch (IOException e)
            {
                throw new InvalidOperationException(e.ToString());
            }
        }

        public bool CrcOk()
        {
            return buffer.CrcOk();
        }
    }
}
