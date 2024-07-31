using HotChocolate.Language;
using HotChocolate.Resolvers;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace S84Account.Service
{
    public static class Util
    {
        public static string GetEnv(string name, string defaultValue = "") {
            return Environment.GetEnvironmentVariable(name) ?? defaultValue;
        }

        public static bool IsBase64String(string base64)
        {
            Span<byte> buffer = new(new byte[base64.Length]);
            return Convert.TryFromBase64String(base64, buffer, out _);
        }

        public static string HASH256(string content)
        {
            byte[] bytes = SHA384.HashData(Encoding.UTF8.GetBytes(content));

            StringBuilder builder = new();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }
            return builder.ToString();
        }
        public static string GetContextData(IResolverContext ctx, string ctxName)
        {
            return ctx.ContextData[ctxName] as string ?? "";
        }

        public static string RandomString(int length)
        {
            char[] _chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray();
            byte[] data = new byte[length];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(data);
            }

            char[] result = new char[length];

            for (int i = 0; i < length; i++)
            {
                int rnd = data[i] % _chars.Length;
                result[i] = _chars[rnd];
            }

            return new string(result);
        }

        public static GraphQLException Exception(HttpStatusCode statusCode, string? message = null)
        {
            return new GraphQLException(ErrorBuilder.New()
            .SetMessage(message ?? statusCode.ToString())
            .SetCode(statusCode.ToString())
            .SetExtension("statusCode", (int)statusCode)
            .Build());
        }

        public static CookieOptions CookieOptions()
        {
            return new CookieOptions
            {
                Path = "/",
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            };
        }

        public static string ToJson(object? obj)
        {
            return obj != null ? JsonSerializer.Serialize(obj) : "";
        }

        public static List<string> GetSelectedFields(IResolverContext resctx)
        {
            return resctx?.Selection?.SyntaxNode?.SelectionSet?.Selections
                .OfType<FieldNode>()
                .Select(field => field.Name.Value)
                .ToList() ?? [];
        }

        public static Expression<Func<T, T>> BindSelect<T>(List<string> selectedFields)
        {
            ParameterExpression parameter = Expression.Parameter(typeof(T), "modelParameter");
            List<MemberBinding> bindings = [];
            Dictionary<string, string> modelFieldMap = GetFieldMap<T>();

            foreach (string field in selectedFields)
            {
                if (modelFieldMap.TryGetValue(field, out string? value))
                {
                    PropertyInfo? member = typeof(T).GetProperty(value);
                    MemberExpression expression = Expression.Property(parameter, value);
                    bindings.Add(Expression.Bind(member!, expression));
                }
            }

            MemberInitExpression initializer = Expression.MemberInit(Expression.New(typeof(T)), bindings);
            return Expression.Lambda<Func<T, T>>(initializer, parameter);
        }

        private static Dictionary<string, string> GetFieldMap<T>()
        {
            return typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .ToDictionary(p => p.Name.ToLower(), p => p.Name);
        }
    }
}
