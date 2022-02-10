using System;
using System.IO;
using System.Linq;

namespace PSSApplication.Core.PatientMonitor
{
    public class SpecificationFile
    {
        private TextReader specReader;

        public SpecificationFile(TextReader specReader)
        {
            this.specReader = specReader;
        }

        public int ReadExponent()
        {
            while (true)
            {
                var line = specReader.ReadLine();
                if (IsExponent(line))
                    return Exponent(line);
            };
        }

        private bool IsExponent(string line)
        {
            if (line == null)
                throw new IvoiProtocolException("Error reading specification file");
            else
                return line.Contains("EXPONENT :");
        }

        private int Exponent(string line)
        {
            try
            {
                var exponent = line
                    .SkipWhile(c => c != ':')
                    .Skip(1)
                    .SkipWhile(c => Char.IsWhiteSpace(c));

                return int.Parse(string.Concat(exponent));
            }
            catch (FormatException e)
            {
                throw new IvoiProtocolException("Error reading specification file: " + e.Message);
            }
        }
    }
}
