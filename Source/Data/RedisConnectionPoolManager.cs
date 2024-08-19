namespace S84Account.Data {
    public class RedisConnectionPoolManager
    {
        private static RedisConnectionPool? _instance;
        private static readonly object _lock = new ();

        //private RedisConnectionPoolManager() { }

        public static RedisConnectionPool GetInstance(string configuration, int poolSize)
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (!(_instance != null))
                    {
                        _instance = new RedisConnectionPool(configuration, poolSize);
                    }
                }
            }
            return _instance;
        }
    }
}
