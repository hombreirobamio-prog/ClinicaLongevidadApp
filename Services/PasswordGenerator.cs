using System;
using System.Security.Cryptography;

namespace ClinicaLongevidadApp.Services
{
    public static class PasswordGenerator
    {
        public static string GenerarTemporal(int length = 12)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@$%&*?";
            char[] result = new char[length];
            byte[] randomBytes = RandomNumberGenerator.GetBytes(length);

            for (int i = 0; i < length; i++)
            {
                result[i] = chars[randomBytes[i] % chars.Length];
            }

            return new string(result);
        }
    }
}
