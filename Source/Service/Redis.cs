using S84Account.Data;
using StackExchange.Redis;

namespace S84Account.Service {
    public static class Redis {
        public static void Handle(RedisConnectionPool pool ,Action<IDatabase> hanleDBRedis) {
            //int poolSize = 100;
            //string configuration = "localhost:7010";
            //using RedisConnectionPool pool = new (configuration, poolSize);
            ConnectionMultiplexer connection = pool.GetConnection();
            try {
                IDatabase db = connection.GetDatabase();
                hanleDBRedis(db);
            } finally { 
                pool.ReturnConnection(connection);
            }

        }
        public static RedisValue GetValue(RedisConnectionPool pool ,Func<IDatabase, RedisValue> hanleDBRedis) {
            //int poolSize = 100;
            RedisValue result;
            //string configuration = "localhost:7010";
            //using RedisConnectionPool pool = new (configuration, poolSize);
            ConnectionMultiplexer connection = pool.GetConnection();
            try {
                IDatabase db = connection.GetDatabase();
                result = hanleDBRedis(db);
            } finally { 
                pool.ReturnConnection(connection);
            }
            return result;
        }
        public static RedisValue[] HashGet(RedisConnectionPool pool ,Func<IDatabase, RedisValue[]> hanleDBRedis) {
            //int poolSize = 100;
            RedisValue[] result;
            //string configuration = "localhost:7010";
            //using RedisConnectionPool pool = new (configuration, poolSize);
            ConnectionMultiplexer connection = pool.GetConnection();
            try {
                IDatabase db = connection.GetDatabase();
                result = hanleDBRedis(db);
            } finally { 
                pool.ReturnConnection(connection);
            }
            return result;
        }
    }
}
