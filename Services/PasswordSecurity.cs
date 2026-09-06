using System;
using System.Security.Cryptography;

namespace ClinicaLongevidadApp.Services
{
    public static class PasswordSecurity
    {
        public static string HashPassword(string password)
        {
            int iterations = 100_000;
            int saltSize = 16; // 128 bits
            int keySize = 32;  // 256 bits

            byte[] salt = RandomNumberGenerator.GetBytes(saltSize);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            byte[] hash = pbkdf2.GetBytes(keySize);

            return $"PBKDF2${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(storedHash))
            {
                return false;
            }

            string[] parts = storedHash.Split('$');
            if (parts.Length != 4 ||
                parts[0] != "PBKDF2" ||
                !int.TryParse(parts[1], out int iterations) ||
                iterations < 10_000 ||
                iterations > 10_000_000)
            {
                return false;
            }

            try
            {
                byte[] salt = Convert.FromBase64String(parts[2]);
                byte[] hash = Convert.FromBase64String(parts[3]);

                if (salt.Length == 0 || hash.Length == 0)
                {
                    return false;
                }

                using var pbkdf2 = new Rfc2898DeriveBytes(
                    password,
                    salt,
                    iterations,
                    HashAlgorithmName.SHA256);
                byte[] testHash = pbkdf2.GetBytes(hash.Length);

                return CryptographicOperations.FixedTimeEquals(hash, testHash);
            }
            catch (FormatException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
