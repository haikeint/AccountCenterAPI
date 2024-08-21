using Microsoft.EntityFrameworkCore;
using HotChocolate.AspNetCore;
using S84Account.Data;
using S84Account.GraphQL.Middleware;
using S84Account.GraphQL.Mutation;
using S84Account.GraphQL.Query;
using S84Account.Service;
using DotNetEnv;

namespace S84Account {
    public class Program {
        public static void Main(string[] args) {
            Env.Load();
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            //builder.WebHost.ConfigureKestrel(serverOptions => {
            //    serverOptions.ListenLocalhost(Env.GetInt("APP_PORT"), (listenOptions) => {
            //        if (Util.IsDevelopment()) listenOptions.UseHttps();
            //    });
            //});

            builder.Services.AddPooledDbContextFactory<MysqlContext>(options =>
                options.UseMySql(Util.GetConnectionString("MYSQL"),
                    new MySqlServerVersion(new Version(8, 0, 37))));

            builder.Services.AddSingleton(option => {
                return RedisConnectionPoolManager.GetInstance(
                    Env.GetString("REDIS_HOST"),
                    Env.GetInt("REDIS_POLLSIZE"));
            });

            builder.Services.AddCors(options => {
                options.AddPolicy("AllowAllOrigins",
                    policy => {
                        policy.WithOrigins(Env.GetString("APP_ORIGINS").Split(';'))
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowCredentials();
                    });
            });

            builder.Services.AddHttpResponseFormatter(_ => CustomHttpResponseFormatter.Instance);
            builder.Services.AddHttpContextAccessor();
            builder.Services
                .AddGraphQLServer()
                //.AllowIntrospection(false)
                .AddQueryType(d => d.Name("Query"))
                .AddMutationType(d => d.Name("Mutation"))
                .AddTypeExtension<AccountQuery>()
                .AddTypeExtension<AccountMutation>()
                .AddTypeExtension<AuthQuery>()
                .AddTypeExtension<AuthMutation>();

            WebApplication app = builder.Build();
            app.UseCors("AllowAllOrigins");
            app.MapGraphQL("/gql")
                .WithOptions(new GraphQLServerOptions {
                    EnableSchemaRequests = Util.IsDevelopment(), //?sdl
                    Tool = {
                        Enable = Util.IsDevelopment(),
                    }
                });
            app.UseRouting();
            app.MapGet("/api/{username}", (string username) => {

            });
            //app.Use(async (context, next) =>
            //{
            //    // Custom logic before GraphQL processing
            //    Console.WriteLine("Before GraphQL");
            //    foreach (var header in context.Request.Headers)
            //    {
            //        Console.WriteLine($"{header.Key}: {header.Value}");
            //    }
            //    await next.Invoke();

            //    // Custom logic after GraphQL processing
            //    Console.WriteLine("After GraphQL");
            //});

            app.Run();
        }
    }
}
