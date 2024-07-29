using S84Account.GraphQL.Middleware;
using S84Account.GraphQL.Resolver;

namespace S84Account.GraphQL.QueryType
{
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
}
