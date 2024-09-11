using HotChocolate.Resolvers;
using Microsoft.EntityFrameworkCore;
using ACAPI.Config;
using ACAPI.Data;
using ACAPI.GraphQL.Middleware;
using ACAPI.Model;
using ACAPI.Helper;
namespace ACAPI.GraphQL.Query
{
    public class AccountQuery : ObjectTypeExtension
    {
        protected override void Configure(IObjectTypeDescriptor descriptor)
        {
            descriptor.Name("Query");

            descriptor.Field("account")
                .Use<AuthorizedMiddleware>()
                .ResolveWith<Resolver>(res => res.GetAccount(default!));

            descriptor.Field("accounts")
                .ResolveWith<Resolver>(res => res.GetAccounts(default!));

            descriptor.Field("test")
                .Use<LoggingMiddleware>()
                .ResolveWith<Resolver>(res => res.GetTest());
        }

        private class Resolver(IDbContextFactory<MysqlContext> contextFactory)
        {
            private readonly IDbContextFactory<MysqlContext> _contextFactory = contextFactory;
            public async Task<AccountModel> GetAccount(IResolverContext resolveCTX)
            {
                string UserId = Util.GetContextData(resolveCTX, EnvirConst.UserId);
                IQueryable<AccountModel> account = _contextFactory.CreateDbContext().Account;
                MysqlContext ctxDB = _contextFactory.CreateDbContext();

                AccountModel? accountModel = await account
                    .Where(a => a.Id == long.Parse(UserId))
                    .Select(Util.BindSelect<AccountModel>(Util.GetSelectedFields(resolveCTX)))
                    .FirstOrDefaultAsync();

                if (accountModel is not null && accountModel.Email is not null)
                {
                    accountModel.Email = MaskEmail(accountModel.Email);
                }
                if (accountModel is not null && accountModel.Phone is not null)
                {
                    accountModel.Phone = MaskPhone(accountModel.Phone);
                }
                if (accountModel is not null && accountModel.Idcode is not null)
                {
                    accountModel.Idcode = MaskIdCode(accountModel.Idcode);
                }
                return accountModel ?? new AccountModel();
            }
            public IQueryable<AccountModel> GetAccounts(IResolverContext resctx)
            {
                IQueryable<AccountModel> account = _contextFactory.CreateDbContext().Account;
                return account.Select(Util.BindSelect<AccountModel>(Util.GetSelectedFields(resctx)));
            }

            public Task<List<AccountModel>> GetTest()
            {
                MysqlContext context = _contextFactory.CreateDbContext();
                return context.Account.ToListAsync();
            }

            private static string MaskEmail(string email)
            {
                int atIndex = email.IndexOf('@');
                int numberOfmask = 6;
                if (atIndex <= 1)
                {
                    return email;
                }

                string name = email[..atIndex];
                string domain = email[atIndex..];

                string maskedName = name[0..3] + new string('*', numberOfmask);
                return maskedName + domain;
            }

            private static string MaskPhone(string phone)
            {
                int numberOfmask = 7;
                string subfix = phone[(phone.Length - 3)..phone.Length];
                return (new string('*', numberOfmask)) + subfix;
            }

            private static string MaskIdCode(string IdCode)
            {
                int numberOfmask = 7;
                string idCodeSubfix = IdCode[(IdCode.Length - 3)..IdCode.Length];
                return (new string('*', numberOfmask)) + idCodeSubfix;
            }
        }

    }
}
