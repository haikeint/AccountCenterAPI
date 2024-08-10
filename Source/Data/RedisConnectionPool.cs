using System;
using System.Collections.Concurrent;
using StackExchange.Redis;

namespace S84Account.Data {
    public class RedisConnectionPool : IDisposable
    {
        private readonly ConcurrentBag<ConnectionMultiplexer> _connections;
        private readonly ConfigurationOptions _options;
        private readonly int _poolSize;
        private bool _disposed = false;

        public RedisConnectionPool(string configuration, int poolSize)
        {
            _options = ConfigurationOptions.Parse(configuration);
            _options.AbortOnConnectFail = false;
            _poolSize = poolSize;
            _connections = [];

            for (int i = 0; i < _poolSize; i++)
            {
                _connections.Add(CreateConnection());
            }
        }

        private ConnectionMultiplexer CreateConnection()
        {
            return ConnectionMultiplexer.Connect(_options);
        }

        public ConnectionMultiplexer GetConnection()
        {
            //if (_connections.TryTake(out var connection))
            //{
            //    return connection;
            //}
            //else
            //{
            //    return CreateConnection();
            //}

            return _connections.TryTake(out var connection) ? connection : CreateConnection();
        }

        public void ReturnConnection(ConnectionMultiplexer connection)
        {
            if (connection != null && connection.IsConnected)
            {
                _connections.Add(connection);
            }
            else
            {
                _connections.Add(CreateConnection());
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _disposed = true;

            while (_connections.TryTake(out var connection))
            {
                connection.Dispose();
            }

            GC.SuppressFinalize(this);
        }
    }
}
