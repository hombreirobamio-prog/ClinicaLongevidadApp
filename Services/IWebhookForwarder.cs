using System.Threading.Tasks;

namespace ClinicaLongevidadApp.Services
{
    public interface IWebhookForwarder
    {
        Task ForwardEventAsync(string jsonPayload, string signature);
    }
}
