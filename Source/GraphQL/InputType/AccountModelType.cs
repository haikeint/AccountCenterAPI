using S84Account.Model;
namespace S84Account.GraphQL.InputType {
    public class AccountModelType : InputObjectType<AccountModel> {
        protected override void Configure(IInputObjectTypeDescriptor<AccountModel> descriptor)
        {
            descriptor.Ignore(x => x.Username);
            descriptor.Ignore(x => x.Id);
        }
    }
}
