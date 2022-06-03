using System;

namespace PSSApplication.Core.PatientMonitor
{
    public class IvoiProtocolException : Exception
    {
        public IvoiProtocolException(string message="") : base(message)
        {
        }
    }
}
