using S84Account.GraphQL.Resolver;

namespace S84Account.GraphQL.MutationType
{
    public class AuthMutation : ObjectTypeExtension<Auth>
    {
        protected override void Configure(IObjectTypeDescriptor<Auth> descriptor)
        {
            descriptor.Name("Mutation");

            descriptor.Field(q => q.Login(default!, default!, default!, default!, default!))
                .Argument("username", a => a.Type<NonNullType<StringType>>())
                .Argument("password", a => a.Type<NonNullType<StringType>>())
                .Argument("rectoken", a => a.Type<NonNullType<StringType>>())
                .Argument("recver", a => a.Type<NonNullType<IntType>>());

            descriptor.Field(q => q.Logout(default!));
            descriptor.Field(q => q.Register(default!, default!, default!, default!));
        }
    }
}
