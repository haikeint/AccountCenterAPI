using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using S84Account.Data;
using S84Account.Model;

namespace S84Account.GraphQL.Query 
{
    public class AuthQuery : ObjectTypeExtension
    {
        protected override void Configure(IObjectTypeDescriptor descriptor)
        {
            descriptor.Name("Query");
            descriptor.Field("checkAccountExist")
                .Argument("username", arg => arg.Type<NonNullType<StringType>>())
                .ResolveWith<Resolver>(res => res.CheckAccountExist(default!));
        }

        private class Resolver(IDbContextFactory<MysqlContext> contextFactory) {
            private readonly IDbContextFactory<MysqlContext> _contextFactory = contextFactory;
            public async Task<bool> CheckAccountExist(string username) {
                Console.WriteLine(username);
                AccountModel? accountModel = null;

                try {
                    MysqlContext ctxDB = _contextFactory.CreateDbContext();
                    accountModel = await ctxDB.Account
                        .Where(account => account.Username == username)
                        .Select(account => new AccountModel {
                            Id = account.Id,
                        })
                        .FirstOrDefaultAsync();
                } catch (Exception) {

                }
                return accountModel != null;
            }
        }
    }
}
