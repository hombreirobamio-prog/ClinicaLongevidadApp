using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ClinicaLongevidadApp.Services
{
    public static class AESService
    {
        // Deriva una clave AES-256 desde la contraseña maestra
        private static byte[] DerivarClave(string password)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes(password)); // 32 bytes
        }

        // CIFRAR
        public static string Cifrar(string textoPlano, string passwordMaestra)
        {
            if (string.IsNullOrEmpty(textoPlano))
                return "";

            byte[] clave = DerivarClave(passwordMaestra);

            using var aes = Aes.Create();
            aes.Key = clave;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV(); // IV aleatorio

            using var ms = new MemoryStream();

            // Escribir IV al inicio
            ms.Write(aes.IV, 0, aes.IV.Length);

            // Escribir datos cifrados
            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                byte[] datosPlano = Encoding.UTF8.GetBytes(textoPlano);
                cs.Write(datosPlano, 0, datosPlano.Length);
                cs.FlushFinalBlock(); // ✅ fuerza el volcado completo
            }

            // Combinar IV + cifrado
            byte[] resultado = ms.ToArray();
            return Convert.ToBase64String(resultado);
        }

        // DESCIFRAR
        public static string Descifrar(string textoCifrado, string passwordMaestra)
        {
            if (string.IsNullOrEmpty(textoCifrado))
                return "";

            byte[] clave = DerivarClave(passwordMaestra);
            byte[] datos = Convert.FromBase64String(textoCifrado);

            using var aes = Aes.Create();
            aes.Key = clave;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            // Leer IV (primeros 16 bytes)
            byte[] iv = new byte[16];
            Array.Copy(datos, iv, 16);
            aes.IV = iv;

            using var ms = new MemoryStream(datos, 16, datos.Length - 16);
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var sr = new StreamReader(cs, Encoding.UTF8);
            return sr.ReadToEnd();
        }
    }
}
