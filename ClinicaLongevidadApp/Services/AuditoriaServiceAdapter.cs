using ClinicaLongevidadApp.Models;

namespace ClinicaLongevidadApp.Services
{
    public class AuditoriaServiceAdapter : IAuditoriaService
    {
        public void RegistrarEvento(AuditoriaEvento evento)
        {
            App.AuditoriaService?.RegistrarEvento(evento);
        }
    }
}
