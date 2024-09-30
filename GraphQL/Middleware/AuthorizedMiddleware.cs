using HotChocolate.Resolvers;
using System.Security.Principal;
using System.Net;
using ACAPI.Helper;
using ACAPI.Wrapper;
using ACAPI.Config;
using Newtonsoft.Json;
using StackExchange.Redis;
using ACAPI.Model;
using DotNetEnv;

namespace ACAPI.GraphQL.Middleware
{
    public class AuthorizedMiddleware(IConnectionMultiplexer redis, FieldDelegate next)
    {
        private readonly FieldDelegate _next = next;
        private readonly IConnectionMultiplexer _redis = redis;
        private readonly int thresholdInMinutes = 30;
        public async Task InvokeAsync(IMiddlewareContext middlewareCTX)
        {
            if (middlewareCTX.ContextData["HttpContext"] is HttpContext httpCTX)
            {
                string jwtToken = httpCTX.Request.Cookies[EnvirConst.AccessToken] ?? throw Util.Exception(HttpStatusCode.Unauthorized);

                IIdentity? identity = JWT.ValidateES384(jwtToken, JWT.ISSUER, httpCTX.Request.Host.ToString());
                if (!(identity?.IsAuthenticated ?? false) || identity.Name is null) throw Util.Exception(HttpStatusCode.Unauthorized);

                LoginWrapper loginPayload = JsonConvert.DeserializeObject<LoginWrapper>(identity.Name) ?? throw Util.Exception(HttpStatusCode.InternalServerError);
                middlewareCTX.ContextData[EnvirConst.UserId] = loginPayload.UserId;

                if(JWT.IsTokenExpiringSoon(jwtToken, thresholdInMinutes) && loginPayload.SessionId is not null) {
                    Session session = new (_redis.GetDatabase());
                    if(!session.Verify(loginPayload.SessionId)) throw Util.Exception(HttpStatusCode.Unauthorized);

                    loginPayload = new () {
                        UserId = loginPayload.UserId,
                        SessionId = session.Create(Value: loginPayload.UserId ?? string.Empty),
                    };
                            
                    jwtToken = JWT.GenerateES384(
                        JsonConvert.SerializeObject(loginPayload), 
                        JWT.ISSUER, 
                        httpCTX.Request.Host.ToString(), 
                        DateTime.UtcNow.AddHours(Env.GetInt("EXPIRE_LOGIN")));

                    httpCTX?.Response.Cookies.Append(EnvirConst.AccessToken, jwtToken, Util.CookieOptions());
                    httpCTX?.Response.Cookies.Append(EnvirConst.AccessTokenExpire, DateTimeOffset.UtcNow.AddHours(Env.GetInt("EXPIRE_LOGIN")).ToString("o"), new CookieOptions
                    {
                        Path = "/",
                        HttpOnly = false,
                        Secure = true,
                        SameSite = SameSiteMode.None,
                        Expires = DateTimeOffset.UtcNow.AddHours(Env.GetInt("EXPIRE_LOGIN"))
                    });
                }

                await _next(middlewareCTX);
            }
            else throw Util.Exception(HttpStatusCode.Unauthorized);
        }
    }
}
