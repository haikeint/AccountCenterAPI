using DotNetEnv;
using System.Security.Cryptography;

namespace S84Account.Service {
    public static class Password {
        private static readonly int ITERATIONS = Env.GetInt("PBKDF2_ITERATIONS");
        public static string Hash(string password) {
            byte[] salt = new byte[16];
            RandomNumberGenerator.Fill(salt);

            using Rfc2898DeriveBytes pbkdf2 = new(password, salt, ITERATIONS, HashAlgorithmName.SHA384);
            byte[] hash = pbkdf2.GetBytes(48);
            byte[] hashBytes = new byte[64];
            Array.Copy(salt, 0, hashBytes, 0, 16);
            Array.Copy(hash, 0, hashBytes, 16, 48);
            return Convert.ToBase64String(hashBytes);
        }

        public static bool Verify(string? password, string? storedHash) {
            if (string.IsNullOrEmpty(password)
                || string.IsNullOrEmpty(storedHash)
                || !Util.IsBase64String(storedHash)) return false;
            byte[] hashBytes = Convert.FromBase64String(storedHash);
            byte[] salt = new byte[16];
            Array.Copy(hashBytes, 0, salt, 0, 16);

            using Rfc2898DeriveBytes pbkdf2 = new(password, salt, ITERATIONS, HashAlgorithmName.SHA384);
            byte[] hash = pbkdf2.GetBytes(48);

            byte[] computedHashBytes = new byte[64];
            Array.Copy(salt, 0, computedHashBytes, 0, 16);
            Array.Copy(hash, 0, computedHashBytes, 16, 48);
            return CryptographicOperations.FixedTimeEquals(hashBytes, computedHashBytes);
        }
    }
}
