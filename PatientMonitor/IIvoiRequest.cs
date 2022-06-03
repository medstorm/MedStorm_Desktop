using System.Collections.Generic;
namespace PSSApplication.Core.PatientMonitor
{
    public interface IIvoiRequest
    {
        ResponseTelegram? Process(CancellationToken token);
    }
}
