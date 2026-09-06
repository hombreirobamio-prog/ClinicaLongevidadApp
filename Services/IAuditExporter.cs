using System.Threading.Tasks;

namespace ClinicaLongevidadApp.Services
{
    public interface IAuditExporter
    {
        Task ExportEventAsync(string eventId, string jsonPayload, string signature);
    }
}
