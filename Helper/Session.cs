using IdGen;
using DotNetEnv;
using StackExchange.Redis;

namespace ACAPI.Helper {
    public class Session(IDatabase redisDB) {
        private readonly IDatabase _redisDB = redisDB;
        private readonly static int EXPIRE = Env.GetInt("EXPIRE_SESSION_DAY");
        public string? Create(string Prefix = "Session_ID", string Value = "", int Expire = 0) {
            Expire = Expire > 0 ? Expire : EXPIRE;
            string session_id = $"{Prefix}_{_generator.CreateId()}";
            return _redisDB.StringSet(session_id, Value) && _redisDB.KeyExpire(session_id, TimeSpan.FromDays(Expire))
                ? session_id 
                : null;
        }

        public bool Verify(string Name, string Value = "", int Extend = 0) {
            RedisValue result = _redisDB.StringGet(Name);
            if(result.HasValue && result.ToString() == Value) {
                if(Extend > 0) {
                    Extend = Extend > 0 ? Extend : EXPIRE;
                    _redisDB.KeyExpire(Name, TimeSpan.FromDays(Extend));
                }
                return true;
            }
            return false;
        }

        private static readonly IdGenerator _generator = CreateGenerator();
        private static IdGenerator CreateGenerator()
        {
            DateTime epoch = new(2024, 8, 2, 0, 0, 0, DateTimeKind.Utc);

            IdStructure idStructure = new(42, 5, 16);
            //2^42 chia 31 557 600 000(1 năm) ~~ 139 năm
            //2^5 = 32 nodeId
            //2^16 = 65536 id mỗi 1ms

            IdGeneratorOptions generatorOptions = new(idStructure, new DefaultTimeSource(epoch));

            return new(Env.GetInt("NODEID"), generatorOptions);
        }
        //public static long CreateId() => _generator.CreateId();
    }
}
