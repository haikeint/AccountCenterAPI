using IdGen;
using System.ComponentModel.DataAnnotations.Schema;
using DotNetEnv;
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
        public static long CreateId() => AccountID.CreateId();

        private static class AccountID {
            private static readonly IdGenerator _generator = CreateGenerator();
            private static IdGenerator CreateGenerator() {
                DateTime epoch = new (2024, 8, 2, 0, 0, 0, DateTimeKind.Utc);

                IdStructure idStructure = new (42, 5, 16);
                //2^42 chia 31 557 600 000(1 năm) ~~ 139 năm
                //2^5 = 32 nodeId
                //2^16 = 65536 id mỗi 1ms

                IdGeneratorOptions generatorOptions = new (idStructure, new DefaultTimeSource(epoch));

                return new (Env.GetInt("NODEID"), generatorOptions);
            }
            public static long CreateId() => _generator.CreateId();
        }
    }
}
