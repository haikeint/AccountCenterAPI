using Microsoft.EntityFrameworkCore;
using S84Account.Data;
using DotNetEnv;
using S84Account.Config;
using S84Account.Model;
using S84Account.Service;
using StackExchange.Redis;
using System.Net;
using HotChocolate.Resolvers;
using System.Security.Cryptography;
using MySqlConnector;
using System.Text.Json.Serialization;

namespace S84Account.GraphQL.Mutation
{
    public class AuthMutation : ObjectTypeExtension
    {
        private class Agrs {
            public static readonly string USERNAME = "username";
            public static readonly string PASSWORD = "password";
            public static readonly string RECTOKEN = "rectoken";
            public static readonly string RECVER = "recver";
            public static readonly string EMAIL = "email";
            public static readonly string RECAPTCHA = "recaptcha";
            //public static readonly string ____ = "____";
            //public static readonly string ____ = "____";
        }
        protected override void Configure(IObjectTypeDescriptor descriptor)
        {
            descriptor.Name("Mutation");

            descriptor.Field("login")
                .Argument(Agrs.USERNAME, arg => arg.Type<NonNullType<StringType>>())
                .Argument(Agrs.PASSWORD, arg => arg.Type<NonNullType<StringType>>())
                .Argument(Agrs.RECTOKEN, arg => arg.Type<NonNullType<StringType>>())
                .Argument(Agrs.RECVER, arg => arg.Type<NonNullType<IntType>>())
                .ResolveWith<Resolver>(res => res.Login(default!));

            descriptor.Field("logout")
                .ResolveWith<Resolver>(res => res.Logout(default!));

            descriptor.Field("register")
                .Argument(Agrs.USERNAME, arg => arg.Type<NonNullType<StringType>>())
                .Argument(Agrs.PASSWORD, arg => arg.Type<NonNullType<StringType>>())
                .Argument(Agrs.EMAIL, arg => arg.Type<NonNullType<StringType>>())
                .Argument(Agrs.RECAPTCHA, arg => arg.Type<NonNullType<StringType>>())
                .ResolveWith<Resolver>(res => res.Register(default!));
        }

        private class Resolver(IDbContextFactory<MysqlContext> contextFactory, RedisConnectionPool redisConnectionPool) {
            private readonly IDbContextFactory<MysqlContext> _contextFactory = contextFactory;
            private readonly RedisConnectionPool _redisPool = redisConnectionPool;

            private static readonly int ITERATIONS = Env.GetInt("PBKDF2_ITERATIONS");
            private static readonly float ACCEPT_SCORE = float.Parse(Env.GetString("RECATPCHA_V3_ACCEPT_SCORE"));

            private static readonly string RECATPCHA_V2_SECRET_KEY = Env.GetString("RECATPCHA_V2_SECRET_KEY");
            private static readonly string RECATPCHA_V3_SECRET_KEY = Env.GetString("RECATPCHA_V3_SECRET_KEY");

            public async Task<bool> Login(IResolverContext ctx) { 
                string username = ctx.ArgumentValue<string>(Agrs.USERNAME);
                string password = ctx.ArgumentValue<string>(Agrs.PASSWORD);
                string rectoken = ctx.ArgumentValue<string>(Agrs.RECTOKEN);
                int recver = ctx.ArgumentValue<int>(Agrs.RECVER);

                IHttpContextAccessor httpContextAccessor = ctx.Service<IHttpContextAccessor>();

                AccountModel? accountModel = null;

                if (!(await VerifyRecaptcha(rectoken, recver))) throw Util.Exception(HttpStatusCode.Forbidden, "Unvalid token");
                try {
                    RedisValue[] accountRedis = Redis.HashGet(_redisPool, redisCTX => {
                        RedisValue[] result = redisCTX.HashGet(username, ["Id", "Password"]);
                        if(result[0].HasValue && result[1].HasValue) {
                            redisCTX.KeyExpire(username, TimeSpan.FromDays(1));
                        }
                        return result;
                    });

                    if (accountRedis[0].HasValue && accountRedis[1].HasValue) {
                        accountModel = new AccountModel {
                            Id = (long)accountRedis[0],
                            Password = accountRedis[1].ToString()
                        };
                    } else {
                        MysqlContext ctxDB = _contextFactory.CreateDbContext();
                        accountModel = await ctxDB.Account
                            .Where(account => account.Username == username)
                            .Select(account => new AccountModel {
                                Id = account.Id,
                                Password = account.Password
                            })
                            .FirstOrDefaultAsync();
                    }

                    if (accountModel is not null) {
                        if (!(accountRedis[0].HasValue && accountRedis[1].HasValue)) {
                            Redis.Handle(_redisPool, redisCTX => {
                                redisCTX.HashSet(username, [
                                    new HashEntry("Id", accountModel.Id),
                                    new HashEntry("Password", accountModel.Password)
                                    ]);
                                redisCTX.KeyExpire(username, TimeSpan.FromDays(1));
                            });
                        }

                        if (VerifyPassword(password, accountModel.Password)) {
                            HttpContext? httpCTX = httpContextAccessor.HttpContext;
                            string host = httpCTX?.Request.Host.ToString() ?? string.Empty;
                            string jwtToken = JWT.GenerateES384(accountModel.Id.ToString() ?? string.Empty, JWT.ISSUER, host);
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

            public async Task<bool> Register(IResolverContext ctx) {
                string username = ctx.ArgumentValue<string>(Agrs.USERNAME);
                string password = ctx.ArgumentValue<string>(Agrs.PASSWORD);
                string email = ctx.ArgumentValue<string>(Agrs.EMAIL);
                string recaptcha = ctx.ArgumentValue<string>(Agrs.RECAPTCHA);

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

            private static async Task<bool> VerifyRecaptcha(string recToken, int version = 2) {
                if(Util.IsDevelopment()) return true;
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

            private static bool VerifyPassword(string? password, string? storedHash) {
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

            private static bool IsUniqueConstraintViolation(DbUpdateException exception) {
                int duplicateCode = 1062;
                if (exception.InnerException is MySqlException mySqlException) {
                    return mySqlException.Number == duplicateCode;
                }
                return false;
            }

            private class ReCaptchaResponse {
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
        }
    }
}
