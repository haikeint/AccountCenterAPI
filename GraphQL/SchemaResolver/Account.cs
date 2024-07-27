using S84Account.Data;
using S84Account.Model;
using Microsoft.EntityFrameworkCore;
using HotChocolate.Resolvers;
using S84Account.GraphQL.Middleware;
using System.Reflection.Metadata;
using S84Account.Config;
using S84Account.Service;

namespace S84Account.GraphQL.SchemaResolver
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

    public class AccountQuery : ObjectTypeExtension<Account>
    {
        protected override void Configure(IObjectTypeDescriptor<Account> descriptor)
        {
            descriptor.Name("Query");

            descriptor.Field(q => q.GetAccount(default!, default!))
                        .Argument("id", a => a.Type<NonNullType<IdType>>());
            descriptor.Field(q => q.GetAccounts(default!));
            descriptor.Field(q => q.GetTest())
                .Use<LoggingMiddleware>();
        }
    }
    public class AccountMutation : ObjectTypeExtension<Account>
    {
        protected override void Configure(IObjectTypeDescriptor<Account> descriptor)
        {
            descriptor.Name("Mutation");

            descriptor.Field(m => m.AddTest(default!, default!))
                .Argument("input", m => m.Type<NonNullType<StringType>>())
                .Use<AuthorizedMiddleware>();

        }
    }
}
