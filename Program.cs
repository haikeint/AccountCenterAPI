namespace S84Account
{
    using S84Account.Model;
    using HotChocolate.AspNetCore;
    using Microsoft.EntityFrameworkCore;
    using S84Account.Data;
    using Microsoft.AspNetCore.Mvc;
    using S84Account.GraphQL;
    using S84Account.GraphQL.Middleware;
    using S84Account.GraphQL.MutationType;
    using S84Account.GraphQL.QueryType;
    using HotChocolate.AspNetCore.Serialization;
    using HotChocolate.Execution;
    using System.Net;
    using Microsoft.AspNetCore.Http.HttpResults;
    using S84Account.Service;
    using Microsoft.AspNetCore.DataProtection;
    using static S84Account.Service.JWT;
    using System.Security.Principal;
    using System.Security.Claims;

    public class Program
    {
        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            builder.Services.AddPooledDbContextFactory<LibraryContext>(options =>
                options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"),
                    new MySqlServerVersion(new Version(8, 0, 37))));

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAllOrigins",
                    policy =>
                    {
                        policy.WithOrigins("https://localhost:3001", "https://localhost:5000")
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowCredentials();
                    });
             
            });

            builder.Services.AddHttpResponseFormatter(_ => CustomHttpResponseFormatter.Instance);
            builder.Services.AddHttpContextAccessor();
            builder.Services
                .AddGraphQLServer()
                .AddQueryType(d => d.Name("Query"))
                .AddMutationType(d => d.Name("Mutation"))
                .AddTypeExtension<AccountQuery>()
                .AddTypeExtension<AccountMutation>()
                .AddTypeExtension<AuthMutation>();


            WebApplication app = builder.Build();
            app.UseCors("AllowAllOrigins");
            app.UseRouting();
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
            app.MapGraphQL("/gql");
            app.Run();
        }
    }
}
