using System;
using System.Text;
using System.Security.Cryptography;

namespace ClinicaLongevidadApp.Services
{
    public static class HashService
    {
        public static string CalcularSHA256(string texto)
        {
            var bytes = Encoding.UTF8.GetBytes(texto);
            var hashBytes = SHA256.HashData(bytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }
    }
}
