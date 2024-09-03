using ACAPI.Model;

namespace ACAPI.GraphQL.InputType
{
    public class UpdateSecureInput
    {
        public string? OldPassword { get; set; }
        public string? NewPassword { get; set; }
        public string? OldPhone { get; set; }
        public string? NewPhone { get; set; }
        public string? OldEmail { get; set; }
        public string? NewEmail { get; set; }
    }

    public class UpdateSecureInputType : InputObjectType<UpdateSecureInput>
    {
        protected override void Configure(IInputObjectTypeDescriptor<UpdateSecureInput> descriptor)
        {

        }
    }

}
