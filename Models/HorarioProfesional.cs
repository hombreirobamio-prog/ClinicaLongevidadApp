using SQLite;

namespace ClinicaLongevidadApp.Models
{
    public class HorarioProfesional
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Profesional { get; set; } = "";
        public int DiaSemana { get; set; }
        public string HoraInicio { get; set; } = "09:00";
        public string HoraFin { get; set; } = "13:00";
        public int IntervaloMinutos { get; set; } = 30;
        public bool Activo { get; set; } = true;
    }
}
