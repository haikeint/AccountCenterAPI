using Microsoft.EntityFrameworkCore;
using ACAPI.Data;
using ACAPI.Config;
using ACAPI.Model;
using StackExchange.Redis;
using System.Net;
using HotChocolate.Resolvers;
using MySqlConnector;
using ACAPI.Helper;
using ACAPI.Wrapper;
using DotNetEnv;
using System.Text.Json;

namespace ACAPI.GraphQL.Mutation
{
    public class AuthMutation : ObjectTypeExtension
    {
        private class Agrs
        {
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
                .Resolve(ctx => Resolver.Logout(ctx.Service<IHttpContextAccessor>()));

            descriptor.Field("register")
                .Argument(Agrs.USERNAME, arg => arg.Type<NonNullType<StringType>>())
                .Argument(Agrs.PASSWORD, arg => arg.Type<NonNullType<StringType>>())
                .Argument(Agrs.EMAIL, arg => arg.Type<NonNullType<StringType>>())
                .Argument(Agrs.RECAPTCHA, arg => arg.Type<NonNullType<StringType>>())
                .ResolveWith<Resolver>(res => res.Register(default!));
        }

        private class Resolver(IDbContextFactory<MysqlContext> contextFactory, IConnectionMultiplexer redis)
        {
            private readonly IDbContextFactory<MysqlContext> _contextFactory = contextFactory;
            private readonly IConnectionMultiplexer _redis = redis;
            public async Task<bool> Login(IResolverContext ctx)
            {
                string username = ctx.ArgumentValue<string>(Agrs.USERNAME);
                string password = ctx.ArgumentValue<string>(Agrs.PASSWORD);
                string rectoken = ctx.ArgumentValue<string>(Agrs.RECTOKEN);
                int recver = ctx.ArgumentValue<int>(Agrs.RECVER);

                IHttpContextAccessor httpContextAccessor = ctx.Service<IHttpContextAccessor>();

                AccountModel? accountModel = null;

                if (!(await Recaptcha.Verify(rectoken, recver))) throw Util.Exception(HttpStatusCode.Forbidden, "Unvalid token");
                try
                {
                    IDatabase redisDB = _redis.GetDatabase();
                    RedisValue[] accountRedis = redisDB.HashGet(username, ["Id", "Password"]);
                    if (accountRedis[0].HasValue && accountRedis[1].HasValue)
                    {
                        redisDB.KeyExpire(username, TimeSpan.FromDays(1));
                        accountModel = new AccountModel
                        {
                            Id = (long)accountRedis[0],
                            Password = accountRedis[1].ToString()
                        };
                    }
                    else
                    {
                        MysqlContext ctxDB = _contextFactory.CreateDbContext();
                        accountModel = await ctxDB.Account
                            .Where(account => account.Username == username)
                            .Select(account => new AccountModel
                            {
                                Id = account.Id,
                                Password = account.Password
                            })
                            .FirstOrDefaultAsync();
                    }

                    if (accountModel is not null)
                    {
                        if (!(accountRedis[0].HasValue && accountRedis[1].HasValue))
                        {
                            redisDB.HashSet(username, [
                                new HashEntry("Id", accountModel.Id),
                                new HashEntry("Password", accountModel.Password)
                            ]);
                            redisDB.KeyExpire(username, TimeSpan.FromDays(1));
                        }

                        if (Password.Verify(password, accountModel.Password))
                        {
                            HttpContext? httpCTX = httpContextAccessor.HttpContext;

                            string host = httpCTX?.Request.Host.ToString() ?? string.Empty;

                            Session session = new (_redis.GetDatabase());

                            LoginWrapper loginPayload = new () {
                                UserId = accountModel.Id.ToString(),
                                SessionId = session.Create(Value: accountModel.Id.ToString() ?? string.Empty),
                            };
                            
                            string jwtToken = JWT.GenerateES384(
                                JsonSerializer.Serialize(loginPayload), 
                                JWT.ISSUER, 
                                host, 
                                DateTime.UtcNow.AddHours(Env.GetInt("EXPIRE_LOGIN_HOUR")));

                            httpCTX?.Response.Cookies.Append(EnvirConst.AccessToken, jwtToken, Util.CookieOptions());
                            httpCTX?.Response.Cookies.Append(EnvirConst.AccessTokenExpire, DateTimeOffset.UtcNow.AddHours(Env.GetInt("EXPIRE_LOGIN_HOUR")).ToString("o"), new CookieOptions
                            {
                                Path = "/",
                                HttpOnly = false,
                                Secure = true,
                                SameSite = SameSiteMode.None,
                                Expires = DateTimeOffset.UtcNow.AddHours(Env.GetInt("EXPIRE_LOGIN_HOUR"))
                            });
                            return true;
                        }
                    }
                }
                catch (Exception)
                {
                    throw Util.Exception(HttpStatusCode.InternalServerError);
                }
                throw Util.Exception(HttpStatusCode.Unauthorized);
            }

            public static bool Logout([Service] IHttpContextAccessor httpContextAccessor)
            {
                try
                {
                    HttpContext? httpCTX = httpContextAccessor.HttpContext;
                    httpCTX?.Response.Cookies.Delete(EnvirConst.AccessToken);
                }
                catch (Exception)
                {
                    throw Util.Exception(HttpStatusCode.InternalServerError);
                }
                return true;
            }

            public async Task<bool> Register(IResolverContext ctx)
            {
                string username = ctx.ArgumentValue<string>(Agrs.USERNAME);
                string password = ctx.ArgumentValue<string>(Agrs.PASSWORD);
                string email = ctx.ArgumentValue<string>(Agrs.EMAIL);
                string recaptcha = ctx.ArgumentValue<string>(Agrs.RECAPTCHA);

                if (!(await Recaptcha.Verify(recaptcha))) throw Util.Exception(HttpStatusCode.Forbidden);
                try
                {
                    AccountModel accountModel = new()
                    {
                        Id = AccountModel.CreateId(),
                        Username = username,
                        Password = Password.Hash(password),
                        Email = email,
                    };
                    MysqlContext dbCTX = _contextFactory.CreateDbContext();
                    dbCTX.Account.Add(accountModel);
                    if (await dbCTX.SaveChangesAsync() > 0) return true;
                }
                catch (DbUpdateException dbEx) when (IsUniqueConstraintViolation(dbEx))
                {
                    throw Util.Exception(HttpStatusCode.Conflict);
                }
                catch (Exception)
                {
                    throw Util.Exception(HttpStatusCode.InternalServerError);
                }
                throw Util.Exception(HttpStatusCode.BadRequest);
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
}
