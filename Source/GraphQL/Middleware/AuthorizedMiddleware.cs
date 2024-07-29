using HotChocolate.Resolvers;
using System.Security.Principal;
using System.Net;
using S84Account.Src.Service;
using S84Account.Src.Config;

namespace S84Account.Src.GraphQL.Middleware
{
    public class AuthorizedMiddleware(FieldDelegate next)
    {
        private readonly FieldDelegate _next = next;

        public async Task InvokeAsync(IMiddlewareContext middlewareCTX)
        {
            if (middlewareCTX.ContextData["HttpContext"] is HttpContext httpCTX)
            {
                string jwtToken = httpCTX.Request.Cookies[EnvirConst.AccessToken] ?? throw Util.Exception(HttpStatusCode.Unauthorized);

                IIdentity? identity = JWT.ValidateES384(jwtToken, JWT.ISSUER, httpCTX.Request.Host.ToString());

                if (!(identity?.IsAuthenticated ?? false)) throw Util.Exception(HttpStatusCode.Unauthorized);

                middlewareCTX.ContextData[EnvirConst.UserId] = identity?.Name;

                await _next(middlewareCTX);

            }
            else throw Util.Exception(HttpStatusCode.Unauthorized);
        }
    }
}
