using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Security.Cryptography;
using S84Account.Service;
using S84Account.Config;
using S84Account.Data;
using S84Account.Model;
using System.Text.Json.Serialization;
using MySqlConnector;
using static System.Net.WebRequestMethods;

namespace S84Account.GraphQL.Resolver
{
    public class ReCaptchaResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("challenge_ts")]
        public DateTime ChallengeTimestamp { get; set; }

        [JsonPropertyName("apk_package_name")]
        public string? ApkPackageName { get; set; }

        [JsonPropertyName("error-codes")]
        public List<string>? ErrorCodes { get; set; }

        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("score")]
        public float? Score { get; set; }
    }

    public class Auth(IDbContextFactory<LibraryContext> contextFactory)
    {
        private readonly IDbContextFactory<LibraryContext> _contextFactory = contextFactory;

        private static readonly int ITERATIONS = 500000;
        private static readonly float ACCEPT_SCORE = 0.5f;

        private static readonly string RECATPCHA_V2_SECRET_KEY = Util.GetEnv("RECATPCHA_V2_SECRET_KEY");
        private static readonly string RECATPCHA_V3_SECRET_KEY = Util.GetEnv("RECATPCHA_V3_SECRET_KEY");

        public async Task<bool> Login(string username, string password, string rectoken, int recver, [Service] IHttpContextAccessor httpContextAccessor)
        {
            if (!(await VerifyRecaptcha(rectoken, recver))) throw Util.Exception(HttpStatusCode.Forbidden);
            try {
                LibraryContext ctxDB = _contextFactory.CreateDbContext();
                AccountModel? accountModel = await ctxDB.Account
                    .Where(account => account.Username == username)
                    .Select(account => new AccountModel
                    {
                        Id = account.Id,
                        Password = account.Password,
                    })
                    .FirstOrDefaultAsync();

                if(accountModel != null && VerifyPassword(password, accountModel.Password)) {
                    HttpContext? httpCTX = httpContextAccessor.HttpContext;
                    string host = httpCTX?.Request.Host.ToString() ?? string.Empty;
                    string jwtToken = JWT.GenerateES384(accountModel.Id.ToString(), JWT.ISSUER, host);
                    httpCTX?.Response.Cookies.Append(EnvirConst.AccessToken, jwtToken, Util.CookieOptions());
                    return true;
                }
            } catch (Exception _) { 
                throw Util.Exception(HttpStatusCode.InternalServerError);
            }
            throw Util.Exception(HttpStatusCode.Unauthorized);
        }

        public bool Logout([Service] IHttpContextAccessor httpContextAccessor) {
            try {
                HttpContext? httpCTX = httpContextAccessor.HttpContext;
                httpCTX?.Response.Cookies.Delete(EnvirConst.AccessToken);
            } catch(Exception _) {
                throw Util.Exception(HttpStatusCode.InternalServerError);
            }
            return true;
        }

        public async Task<bool> Register(string username, string password, string email, string recaptcha) {
            if(! (await VerifyRecaptcha(recaptcha))) throw Util.Exception(HttpStatusCode.Forbidden);
            try {
                AccountModel accountModel = new() {
                    Username = username,
                    Password = HashPassword(password),
                    Email = email,
                };
                LibraryContext dbCTX = _contextFactory.CreateDbContext();
                dbCTX.Account.Add(accountModel);
                if(await dbCTX.SaveChangesAsync() > 0) return true;
            } catch (DbUpdateException dbEx) when (IsUniqueConstraintViolation(dbEx)) {
                throw Util.Exception(HttpStatusCode.Conflict);
            } catch (Exception _) {
                throw Util.Exception(HttpStatusCode.InternalServerError);
            }
            throw Util.Exception(HttpStatusCode.BadRequest);
        }

        public async Task<bool> CheckAccountExist(string username) {
            AccountModel? accountModel = null;

            try {
                LibraryContext ctxDB = _contextFactory.CreateDbContext();
                accountModel = await ctxDB.Account
                    .Where(account => account.Username == username)
                    .Select(account => new AccountModel
                    {
                        Id = account.Id,
                    })
                    .FirstOrDefaultAsync();
            } catch(Exception _) {

            }
            return accountModel != null;
        }

        private static string HashPassword(string password)
        {
            byte[] salt = new byte[16];
            RandomNumberGenerator.Fill(salt);

            using Rfc2898DeriveBytes pbkdf2 = new(password, salt, ITERATIONS, HashAlgorithmName.SHA384);
            byte[] hash = pbkdf2.GetBytes(48);
            byte[] hashBytes = new byte[64];
            Array.Copy(salt, 0, hashBytes, 0, 16);
            Array.Copy(hash, 0, hashBytes, 16, 48);
            return Convert.ToBase64String(hashBytes);
        }

        private static bool VerifyPassword(string password, string storedHash)
        {
            if (!Util.IsBase64String(storedHash)) return false;
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

        private static async Task<bool> VerifyRecaptcha(string recToken, int version = 2) {
            string verifyURL = "https://www.google.com/recaptcha/api/siteverify";

            Dictionary<string, string> formData = new ()
            {
                { "secret", version == 2 ? RECATPCHA_V2_SECRET_KEY : RECATPCHA_V3_SECRET_KEY },
                { "response", recToken }
            };
            FormUrlEncodedContent content = new (formData);

            try
            {
                using HttpClient client = new ();
                HttpResponseMessage response = await client.PostAsync(verifyURL, content);

                if(response.EnsureSuccessStatusCode().StatusCode != HttpStatusCode.OK) return false;
                ReCaptchaResponse? responseBody = await response.Content.ReadFromJsonAsync<ReCaptchaResponse>();
                return version == 2 ? responseBody?.Success ?? false : (responseBody?.Score ?? 0.0) >= ACCEPT_SCORE;
            }
            catch (HttpRequestException _)
            {
                //Console.WriteLine($"Request error: {_.Message}");
            }
            return false;
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        {
            int duplicateCode = 1062;
            if (exception.InnerException is MySqlException mySqlException)
            {
                return mySqlException.Number == duplicateCode;
            }
            return false;
        }
    }
}
