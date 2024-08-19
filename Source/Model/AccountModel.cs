using IdGen;
using System.ComponentModel.DataAnnotations.Schema;

namespace S84Account.Model
{
    [Table("account")]
    public class AccountModel
    {
        private enum Genders {
            Male = 1,
            Female = 2,
            Other = 0
        }

        public long Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Gender { get; set; } = (int)Genders.Other;
        public int Phone { get; set; }
        public string Address { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Idcode { get; set; } = string.Empty;
        public static long CreateId(int nodeId = 0) {
            if(!(nodeId != 0)) nodeId = AccountID.NodeId;
            return long.Parse(nodeId.ToString() + AccountID.CreateId());
        }

        private static class AccountID {
            public static int NodeId = 1000;
            private static readonly IdGenerator _generator = CreateGenerator();
            private static IdGenerator CreateGenerator() {
                DateTime epoch = new (2024, 8, 2, 0, 0, 0, DateTimeKind.Utc);
                IdStructure idStructure = new (44, 0, 19);
                IdGeneratorOptions generatorOptions = new (idStructure, new DefaultTimeSource(epoch));
                return new (0, generatorOptions);
            }
            public static string CreateId() => _generator.CreateId().ToString();
        }
    }
}
