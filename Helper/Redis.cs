using ACAPI.Data;
using StackExchange.Redis;

namespace ACAPI.Helper
{
    public static class Redis
    {
        public static void Handle(RedisConnectionPool pool, Action<IDatabase> hanleDBRedis)
        {
            //int poolSize = 100;
            //string configuration = "localhost:7010";
            //using RedisConnectionPool pool = new (configuration, poolSize);
            ConnectionMultiplexer connection = pool.GetConnection();
            try
            {
                IDatabase db = connection.GetDatabase();
                hanleDBRedis(db);
            }
            finally
            {
                pool.ReturnConnection(connection);
            }

        }
        public static T GetValue<T>(RedisConnectionPool pool, Func<IDatabase, T> hanleDBRedis)
        {
            //int poolSize = 100;
            //string configuration = "localhost:7010";
            //using RedisConnectionPool pool = new (configuration, poolSize);

            T result;
            ConnectionMultiplexer connection = pool.GetConnection();
            try
            {
                IDatabase db = connection.GetDatabase();
                result = hanleDBRedis(db);
            }
            finally
            {
                pool.ReturnConnection(connection);
            }
            return result;
        }
    }
}
