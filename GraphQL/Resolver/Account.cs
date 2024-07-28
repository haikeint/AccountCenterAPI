using S84Account.Data;
using S84Account.Model;
using Microsoft.EntityFrameworkCore;
using HotChocolate.Resolvers;
using S84Account.GraphQL.Middleware;
using System.Reflection.Metadata;
using S84Account.Config;
using S84Account.Service;

namespace S84Account.GraphQL.Resolver
{
    public class Account(IDbContextFactory<LibraryContext> contextFactory)
    {
        private readonly IDbContextFactory<LibraryContext> _contextFactory = contextFactory;

        public IQueryable<AccountModel> GetAccount(int id, IResolverContext resctx)
        {
            IQueryable<AccountModel> account = _contextFactory.CreateDbContext().Account;
            return account
                .Where(a => a.Id == id)
                .Select(Util.BindSelect<AccountModel>(Util.GetSelectedFields(resctx)));
        }

        public IQueryable<AccountModel> GetAccounts(IResolverContext resctx)
        {
            IQueryable<AccountModel> account = _contextFactory.CreateDbContext().Account;
            return account.Select(Util.BindSelect<AccountModel>(Util.GetSelectedFields(resctx)));
        }

        public Task<List<AccountModel>> GetTest()
        {
            LibraryContext context = _contextFactory.CreateDbContext();
            return context.Account.ToListAsync();
        }

        public string AddTest(string input, IResolverContext resolveCTX)
        {
            Console.WriteLine(input);
            string UserId = Util.GetContextData(resolveCTX, EnvirConst.UserId);
            return UserId;
        }
    }
}
