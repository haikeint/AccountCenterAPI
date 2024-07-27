namespace S84Account
{
    using S84Account.Model;
    using HotChocolate.AspNetCore;
    using Microsoft.EntityFrameworkCore;
    using S84Account.Data;
    using Microsoft.AspNetCore.Mvc;
    using S84Account.GraphQL;
    using S84Account.GraphQL.Middleware;
    using S84Account.GraphQL.SchemaResolver;
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
            app.MapGet("/login", () =>
            {
                //return Util.HashPassword("haikeint", "user1");
                //return Util.HashPassword("~!@#$%^&*()_+", "123123");
            });
            app.MapGet("/hash", () =>
            {
                //string token = "cgzQ+ImVouHmsZhSnEsCs/xhNOq+0YWMvgjQ4NgE1AEzc8hxmUrVJhWuL64XkXtyQYb1fmaGtaOwovbjydO/7g==";

                //return Util.VerifyPassword(token, "haikeint");
                //return Util.HashPassword("~!@#$%^&*()_+", "123123");
            });
         
            app.MapGet("/123", () =>
            {
                return JWT.IsTokenExpiringSoon("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1bmlxdWVfbmFtZSI6ImhhaWtlaW50IiwibmJmIjoxNzIxNTcxMjEyLCJleHAiOjE3MjE1NzQ4MTIsImlhdCI6MTcyMTU3MTIxMiwiaXNzIjoiczg0LnZuIiwiYXVkIjoieW91cl9hdWRpZW5jZSJ9.pC8PavfXqDmZtYWqm92Fp0TYOMA68tDn6DP5OqmSm1A", 1);
            });

            app.MapGet("/makees384", () =>
            {
                string content = "507f191e810c19729de860ea";
                return JWT.GenerateES384(content, "s84.vn", "your_audience");
            });
            app.MapGet("/valides384", () =>
            {
                string token = "eyJ1bmlxdWVfbmFtZSI6IjUwN2YxOTFlODEwYzE5NzI5ZGU4NjBlYSIsIm5iZiI6MTcyMTYzMTI0MiwiZXhwIjoxNzIxNjM0ODQyLCJpYXQiOjE3MjE2MzEyNDIsImlzcyI6InM4NC52biIsImF1ZCI6InlvdXJfYXVkaWVuY2UifQ.gG_UGONTWFG7GBLbVBr2CbvUzsYjiod4Hi1k-HgQnhfFhGJTWnd1q5JbdzrCIitw-mmu1qJ94Mw2TQFIJ804JcI4OJzr-sF6589Y8V3qjnDTbFXZ6mB9hZw9vYKh5mkQ";
                IIdentity? identity = JWT.ValidateES384(token, "s84.vn", "your_audience");
                //principal?.Identity?.IsAuthenticated
                if(identity?.IsAuthenticated ?? false)
                {
                    return "ok";
                }
                return "xac thuc that bai";
            });
            app.MapGet("/accounts", async (IDbContextFactory<LibraryContext> contextFactory) =>
            {
                await using var context = contextFactory.CreateDbContext();
                return await context.Account.ToListAsync();
            });

            app.Run();
        }
    }
}
