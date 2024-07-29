using S84Account.Src.GraphQL.Middleware;
using S84Account.Src.GraphQL.Resolver;

namespace S84Account.Src.GraphQL.MutationType
{
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
