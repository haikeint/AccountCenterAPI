using S84Account.GraphQL.Middleware;
using S84Account.Model;
using S84Account.GraphQL.InputType;
using HotChocolate.Resolvers;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using S84Account.Data;
using S84Account.Config;
using S84Account.Service;
using Microsoft.EntityFrameworkCore.ChangeTracking;
namespace S84Account.GraphQL.Mutation
{
    public class AccountMutation : ObjectTypeExtension
    {
        protected override void Configure(IObjectTypeDescriptor descriptor)
        {
            descriptor.Name("Mutation");

            descriptor.Field("update")
                .Argument("account", a=> a.Type<NonNullType<AccountModelType>>())
                .Use<AuthorizedMiddleware>()
            .ResolveWith<Resolver>(res => res.UpdateInfo(default!));
        }

        private class Resolver(IDbContextFactory<MysqlContext> contextFactory) {
            private readonly IDbContextFactory<MysqlContext> _contextFactory = contextFactory;

            public bool UpdateInfo(IResolverContext ctx)
            {
                string UserId = Util.GetContextData(ctx, EnvirConst.UserId);
                AccountModel accountInput = ctx.ArgumentValue<AccountModel>("account");
                accountInput.Id = long.Parse(UserId);

                MysqlContext mysqlCTX = _contextFactory.CreateDbContext();
                mysqlCTX.Account.Attach(accountInput);
                foreach(PropertyEntry property in mysqlCTX.Entry(accountInput).Properties) {
                    property.IsModified = property.CurrentValue is not null
                        && property.Metadata.Name != "Id";
                }
                
                int rowAffect = mysqlCTX.SaveChanges();
                Console.WriteLine(rowAffect);
                return true;
            }
        }
    }
}
