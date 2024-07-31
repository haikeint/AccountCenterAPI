using S84Account.GraphQL.Resolver;
namespace S84Account.GraphQL.QueryType {
    public class AuthQuery : ObjectTypeExtension<Auth>
    {
        protected override void Configure(IObjectTypeDescriptor<Auth> descriptor)
        {
            descriptor.Name("Query");
            descriptor.Field(q => q.CheckAccountExist(default!));
        }
    }
}
