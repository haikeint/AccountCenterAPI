using System.ComponentModel.DataAnnotations.Schema;

namespace ACAPI.Model {

    [Table("phone_prefixes")]
    public class PhonePrefixModel
    {
        public long? Id { get; set; }
        public string? Prefix { get; set; }
    
    }
}
