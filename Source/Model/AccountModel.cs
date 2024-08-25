using IdGen;
using System.ComponentModel.DataAnnotations.Schema;
using DotNetEnv;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using System;
namespace S84Account.Model
{
    [Table("account")]
    public class AccountModel
    {
        public enum Genders {
            Female = 0,
            Male = 1,
            Other = 2
        }

        private DateTime? _birthdate; 
        public long? Id { get; set; }
        public string? Username { get; set; }
        [GraphQLIgnore]
        public string? Password { get; set; }

        public string? Fullname { get; set; }

        public int? Gender { get; set; }

        //public DateTime? Birthdate { get; set; }

        public string? Phone { get; set; }

        public string? Address { get; set; }

        public string? Email { get; set; }
        public string? Idcode { get; set; }

        public DateTime? Birthdate {
            get => _birthdate;
            set { 
                _birthdate = value is not null ? TimeZoneInfo.ConvertTime((DateTime)value, _timeZoneInfo) : null;
            }
        }

        [Column("created_at")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public DateTime? Createdat { get; set; }

        [Column("updated_at")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime? Updatedat { get; set; }

        public static long CreateId() => AccountID.CreateId();

        private static readonly TimeZoneInfo _timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

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
