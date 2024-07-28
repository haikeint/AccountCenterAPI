using Microsoft.EntityFrameworkCore;
using S84Account.Data;
using S84Account.Model;
using System.Net;
using S84Account.Service;
using S84Account.Config;
using System.Security.Cryptography;

namespace S84Account.GraphQL.SchemaResolver {
    public class Auth(IDbContextFactory<LibraryContext> contextFactory) {
        private readonly IDbContextFactory<LibraryContext> _contextFactory = contextFactory;
        private static readonly int _iterations = 500000;

        public async Task<bool> Authencation(string username, string password, [Service] IHttpContextAccessor httpContextAccessor) {
            LibraryContext context = _contextFactory.CreateDbContext();

            AccountModel accountModel = await context.Account
                .Where(account => account.Username == username)
                .Select(account => new AccountModel {
                    Id = account.Id,
                    Password = account.Password,
                })
                .FirstOrDefaultAsync() ?? throw Util.Exception(HttpStatusCode.Unauthorized);

            if (!VerifyPassword(password, accountModel.Password)) throw Util.Exception(HttpStatusCode.Unauthorized);

            HttpContext? httpCTX = httpContextAccessor.HttpContext;
            string host = httpCTX?.Request.Host.ToString() ?? string.Empty;
            string jwtToken = JWT.GenerateES384(accountModel.Id.ToString(), JWT.ISSUER, host);
            httpCTX?.Response.Cookies.Append(EnvirConst.AccessToken, jwtToken, Util.CookieOptions());
            return true;
        }

        private static string HashPassword(string password) {
            byte[] salt = new byte[16];
            RandomNumberGenerator.Fill(salt);

            using Rfc2898DeriveBytes pbkdf2 = new(password, salt, _iterations, HashAlgorithmName.SHA384);
            byte[] hash = pbkdf2.GetBytes(48);
            byte[] hashBytes = new byte[64];
            Array.Copy(salt, 0, hashBytes, 0, 16);
            Array.Copy(hash, 0, hashBytes, 16, 48);
            return Convert.ToBase64String(hashBytes);
        }

        private static bool VerifyPassword(string password, string storedHash) {
            if (!Util.IsBase64String(storedHash)) return false;
            byte[] hashBytes = Convert.FromBase64String(storedHash);
            byte[] salt = new byte[16];
            Array.Copy(hashBytes, 0, salt, 0, 16);

            using Rfc2898DeriveBytes pbkdf2 = new(password, salt, _iterations, HashAlgorithmName.SHA384);
            byte[] hash = pbkdf2.GetBytes(48);

            byte[] computedHashBytes = new byte[64];
            Array.Copy(salt, 0, computedHashBytes, 0, 16);
            Array.Copy(hash, 0, computedHashBytes, 16, 48);
            return CryptographicOperations.FixedTimeEquals(hashBytes, computedHashBytes);
        }
    }
}
