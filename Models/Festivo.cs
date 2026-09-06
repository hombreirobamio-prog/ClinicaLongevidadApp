using System;
using SQLite;

namespace ClinicaLongevidadApp.Models
{
    public class Festivo
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public DateTime Fecha { get; set; }
        public string Nombre { get; set; } = "";
        public string Tipo { get; set; } = "Nacional";
        public bool Activo { get; set; } = true;
    }
}
