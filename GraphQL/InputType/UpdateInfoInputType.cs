namespace ACAPI.GraphQL.InputType
{

    public class UpdateInfoInput
    {
        public string? Fullname { get; set; }
        public int? Gender { get; set; }
        public DateTime? Birthdate { get; set; }
        public string? Address { get; set; }
    }

    public class UpdateInfoInputType : InputObjectType<UpdateInfoInput>
    {
        protected override void Configure(IInputObjectTypeDescriptor<UpdateInfoInput> descriptor)
        {

        }
    }
}
