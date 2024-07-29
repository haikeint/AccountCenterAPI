using S84Account.Src.GraphQL.Resolver;

namespace S84Account.Src.GraphQL.MutationType
{
    public class AuthMutation : ObjectTypeExtension<Auth>
    {
        protected override void Configure(IObjectTypeDescriptor<Auth> descriptor)
        {
            descriptor.Name("Mutation");

            descriptor.Field(q => q.Authencation(default!, default!, default!))
                .Argument("username", a => a.Type<NonNullType<StringType>>())
                .Argument("password", a => a.Type<NonNullType<StringType>>());
        }
    }
}
