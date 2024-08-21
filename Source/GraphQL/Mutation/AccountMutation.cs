using S84Account.GraphQL.Middleware;
using S84Account.Model;
using S84Account.GraphQL.InputType;
using HotChocolate.Resolvers;
namespace S84Account.GraphQL.Mutation
{
    public class AccountMutation : ObjectTypeExtension
    {
        protected override void Configure(IObjectTypeDescriptor descriptor)
        {
            descriptor.Name("Mutation");

            descriptor.Field("addTest1")
                .Argument("account", a=> a.Type<NonNullType<AccountModelType>>())
                .Use<AuthorizedMiddleware>()
                .ResolveWith<Resolver>(res => res.AddTest1(default!));
        }

        private class Resolver {
            public string AddTest1(AccountModel account)
            {
                return $"{account.Username} {account.Email}";
                //string UserId = Util.GetContextData(resolveCTX, EnvirConst.UserId);
                //return UserId;
            }
        }
    }
}
