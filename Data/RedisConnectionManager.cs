using StackExchange.Redis;
using DotNetEnv;
using System.Data.Common;

namespace ACAPI.Data {
    public class RedisConnectionManager {
        private static string ConnectionString = string.Empty;

        private static readonly Lazy<ConnectionMultiplexer> _lazyConnection = new Lazy<ConnectionMultiplexer>(() => {
            ConfigurationOptions config = new () {
                AbortOnConnectFail = false,
                ConnectRetry = 3,
                ReconnectRetryPolicy = new ExponentialRetry(5000),
                ConnectTimeout = 10000,
                SyncTimeout = 10000,
                DefaultDatabase = 0,
                AllowAdmin = true,   // Để kích hoạt các lệnh quản trị nếu cần
                CommandMap = CommandMap.Create(
                [
                    "SUBSCRIBE", "UNSUBSCRIBE", "PSUBSCRIBE", "PUNSUBSCRIBE"  // Tắt các lệnh không tương thích với Redis Cluster
                ], 
                available: false),
                    //UseSsl = false,  // Nếu cluster của bạn không dùng SSL thì giữ là false
                    //Password = "yourpassword",  // Nếu Redis Cluster yêu cầu mật khẩu
            };

            foreach (string endpoint in ConnectionString.Split(','))
            {
                config.EndPoints.Add(endpoint);
            }

            return ConnectionMultiplexer.Connect(config);
        });

        public static ConnectionMultiplexer GetInstance(string connectionString) {
            ConnectionString = connectionString;
            return _lazyConnection.Value;
        }
    }

}
