using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Security.Cryptography;
using S84Account.Service;
using S84Account.Config;
using S84Account.Data;
using S84Account.Model;
using System.Text.Json.Serialization;
using MySqlConnector;
using StackExchange.Redis;
using DotNetEnv;

namespace S84Account.GraphQL.Resolver {
    public class ReCaptchaResponse {
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

    public class Auth(IDbContextFactory<MysqlContext> contextFactory, RedisConnectionPool redisConnectionPool) {
        private readonly IDbContextFactory<MysqlContext> _contextFactory = contextFactory;
        private readonly RedisConnectionPool _redisPool = redisConnectionPool;

        private static readonly int ITERATIONS = Env.GetInt("PBKDF2_ITERATIONS");
        private static readonly float ACCEPT_SCORE = float.Parse(Env.GetString("RECATPCHA_V3_ACCEPT_SCORE"));

        private static readonly string RECATPCHA_V2_SECRET_KEY = Env.GetString("RECATPCHA_V2_SECRET_KEY");
        private static readonly string RECATPCHA_V3_SECRET_KEY = Env.GetString("RECATPCHA_V3_SECRET_KEY");

        public async Task<bool> Login(string username, string password, string rectoken, int recver, [Service] IHttpContextAccessor httpContextAccessor) {
            AccountModel? accountModel = null;

            if (!(await VerifyRecaptcha(rectoken, recver))) throw Util.Exception(HttpStatusCode.Forbidden, "Unvalid token");
            try {
                RedisValue[] accountRedis = Redis.HashGet(_redisPool, redisCTX => {
                    return redisCTX.HashGet(username, ["Id", "Password"]);
                });

                if (accountRedis[0].HasValue && accountRedis[1].HasValue) {
                    Console.WriteLine("__Redis");
                    accountModel = new AccountModel {
                        Id = (long)accountRedis[0],
                        Password = accountRedis[1].ToString()
                    };
                } else {
                    Console.WriteLine("__Mysql");
                    MysqlContext ctxDB = _contextFactory.CreateDbContext();
                    accountModel = await ctxDB.Account
                        .Where(account => account.Username == username)
                        .Select(account => new AccountModel {
                            Id = account.Id,
                            Username = account.Username,
                            Password = account.Password,
                        })
                        .FirstOrDefaultAsync();
                }

                if (accountModel != null) {
                    Redis.Handle(_redisPool, redisCTX => {
                        redisCTX.HashSet(accountModel.Username, [
                            new HashEntry("Id", accountModel.Id),
                            new HashEntry("Password", accountModel.Password)
                            ]);
                        redisCTX.KeyExpire(accountModel.Username, TimeSpan.FromDays(1));
                    });
                    if (VerifyPassword(password, accountModel.Password)) {
                        HttpContext? httpCTX = httpContextAccessor.HttpContext;
                        string host = httpCTX?.Request.Host.ToString() ?? string.Empty;
                        string jwtToken = JWT.GenerateES384(accountModel.Id.ToString(), JWT.ISSUER, host);
                        httpCTX?.Response.Cookies.Append(EnvirConst.AccessToken, jwtToken, Util.CookieOptions());
                        return true;
                    }

                }
            } catch (Exception) {
                throw Util.Exception(HttpStatusCode.InternalServerError);
            }
            throw Util.Exception(HttpStatusCode.Unauthorized);
        }

        public bool Logout([Service] IHttpContextAccessor httpContextAccessor) {
            try {
                HttpContext? httpCTX = httpContextAccessor.HttpContext;
                httpCTX?.Response.Cookies.Delete(EnvirConst.AccessToken);
            } catch (Exception) {
                throw Util.Exception(HttpStatusCode.InternalServerError);
            }
            return true;
        }

        public async Task<bool> Register(string username, string password, string email, string recaptcha) {
            if (!(await VerifyRecaptcha(recaptcha))) throw Util.Exception(HttpStatusCode.Forbidden);
            try {
                AccountModel accountModel = new() {
                    Id = AccountModel.CreateId(),
                    Username = username,
                    Password = HashPassword(password),
                    Email = email,
                };
                MysqlContext dbCTX = _contextFactory.CreateDbContext();
                dbCTX.Account.Add(accountModel);
                if (await dbCTX.SaveChangesAsync() > 0) return true;
            } catch (DbUpdateException dbEx) when (IsUniqueConstraintViolation(dbEx)) {
                throw Util.Exception(HttpStatusCode.Conflict);
            } catch (Exception) {
                throw Util.Exception(HttpStatusCode.InternalServerError);
            }
            throw Util.Exception(HttpStatusCode.BadRequest);
        }

        public async Task<bool> CheckAccountExist(string username) {
            AccountModel? accountModel = null;

            try {
                MysqlContext ctxDB = _contextFactory.CreateDbContext();
                accountModel = await ctxDB.Account
                    .Where(account => account.Username == username)
                    .Select(account => new AccountModel {
                        Id = account.Id,
                    })
                    .FirstOrDefaultAsync();
            } catch (Exception) {

            }
            return accountModel != null;
        }

        private static string HashPassword(string password) {
            byte[] salt = new byte[16];
            RandomNumberGenerator.Fill(salt);

            using Rfc2898DeriveBytes pbkdf2 = new(password, salt, ITERATIONS, HashAlgorithmName.SHA384);
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

            using Rfc2898DeriveBytes pbkdf2 = new(password, salt, ITERATIONS, HashAlgorithmName.SHA384);
            byte[] hash = pbkdf2.GetBytes(48);

            byte[] computedHashBytes = new byte[64];
            Array.Copy(salt, 0, computedHashBytes, 0, 16);
            Array.Copy(hash, 0, computedHashBytes, 16, 48);
            return CryptographicOperations.FixedTimeEquals(hashBytes, computedHashBytes);
        }

        private static async Task<bool> VerifyRecaptcha(string recToken, int version = 2) {
            string verifyURL = "https://www.google.com/recaptcha/api/siteverify";
            Dictionary<string, string> formData = new()
            {
                { "secret", version == 2 ? RECATPCHA_V2_SECRET_KEY : RECATPCHA_V3_SECRET_KEY },
                { "response", recToken }
            };
            FormUrlEncodedContent content = new(formData);
            try {
                using HttpClient client = new();
                HttpResponseMessage response = await client.PostAsync(verifyURL, content);

                if (response.EnsureSuccessStatusCode().StatusCode != HttpStatusCode.OK) return false;
                ReCaptchaResponse? responseBody = await response.Content.ReadFromJsonAsync<ReCaptchaResponse>();
                return version == 2 ? responseBody?.Success ?? false : (responseBody?.Score ?? 0.0)*100 >= ACCEPT_SCORE *100;
            } catch (HttpRequestException) {
                //Console.WriteLine($"Request error: {_.Message}");
            }
            return false;
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException exception) {
            int duplicateCode = 1062;
            if (exception.InnerException is MySqlException mySqlException) {
                return mySqlException.Number == duplicateCode;
            }
            return false;
        }
    }
}
