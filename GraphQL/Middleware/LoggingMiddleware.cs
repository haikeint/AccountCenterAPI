using HotChocolate.Resolvers;

namespace ACAPI.GraphQL.Middleware
{
    public class LoggingMiddleware(FieldDelegate next)
    {
        private readonly FieldDelegate _next = next;

        public async Task InvokeAsync(IMiddlewareContext context)
        {
            // Trước khi xử lý
            Console.WriteLine("Before resolving field");
            // Gọi middleware tiếp theo trong pipeline
            await _next(context);

            // Sau khi xử lý
            Console.WriteLine("After resolving field");
        }
    }
}
