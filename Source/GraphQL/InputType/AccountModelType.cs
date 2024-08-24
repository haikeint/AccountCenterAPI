using S84Account.Model;
using System.Security.Principal;
namespace S84Account.GraphQL.InputType {
    public class AccountModelType : InputObjectType<AccountModel> {
        protected override void Configure(IInputObjectTypeDescriptor<AccountModel> descriptor)
        {
            descriptor.Ignore(account => account.Username);
            descriptor.Ignore(account => account.Id);
            descriptor.Field(account => account.Password);
        }
    }
}
