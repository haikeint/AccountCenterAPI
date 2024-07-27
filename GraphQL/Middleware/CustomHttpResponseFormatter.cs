using HotChocolate.AspNetCore.Serialization;
using HotChocolate.Execution;
using System.Net;

namespace S84Account.GraphQL.Middleware
{
    public class CustomHttpResponseFormatter(HttpResponseFormatterOptions options) : DefaultHttpResponseFormatter(options)
    {
        protected override HttpStatusCode OnDetermineStatusCode(
            IQueryResult result, FormatInfo format,
            HttpStatusCode? proposedStatusCode)
        {
            //if (result.Errors?.Count > 0 &&
            //    result.Errors.Any(error => error.Code == "SOME_AUTH_ISSUE"))
            //{
            //    return HttpStatusCode.OK;
            //    //return HttpStatusCode.Forbidden;
            //}
            //Console.WriteLine("chay qua day");
            //// In all other cases let Hot Chocolate figure out the
            //// appropriate status code.
            //return base.OnDetermineStatusCode(result, format, proposedStatusCode);

            return HttpStatusCode.OK;
        }

        public static readonly CustomHttpResponseFormatter Instance = new(new HttpResponseFormatterOptions());
    }
}
