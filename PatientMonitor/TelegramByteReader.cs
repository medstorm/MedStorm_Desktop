using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace PSSApplication.Core.PatientMonitor
{
    public class TelegramByteReader
    {
        private BinaryReader _binaryReader;
        private CrcBuffer _buffer = new CrcBuffer();
        CancellationToken _cancelationToken;
        public TelegramByteReader(BinaryReader reader, CancellationToken token)
        {
            _cancelationToken = token;
            _binaryReader = reader;
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
                while (!_cancelationToken.IsCancellationRequested)
                {

                    try
                    {
                        byte result = _binaryReader.ReadByte();
                        _buffer.Add(result);
                        Debug.WriteLine("0x{0:X2} - d:{1:d}", result, result);
                        return result;
                    }
                    catch (TimeoutException ex)
                    {
                        Task.Delay(100);
                    }
                }
                throw new Exception("TelegramByteReader - read has been canceld");
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
            return _buffer.CrcOk();
        }
    }
}
