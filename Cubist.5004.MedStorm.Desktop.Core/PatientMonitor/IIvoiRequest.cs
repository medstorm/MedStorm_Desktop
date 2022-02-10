using System.Collections.Generic;
namespace PSSApplication.Core.PatientMonitor
{
    public interface IIvoiRequest
    {
        IResponseTelegram Process();
    }
}
