using Microsoft.EntityFrameworkCore;
using ACAPI.Data;
using ACAPI.Service;
using ACAPI.Helper;
using Newtonsoft.Json;

namespace ACAPI.Controller {

    using Microsoft.AspNetCore.Mvc;
    using StackExchange.Redis;
    using System.Net;
    using System.Security.Principal;
    using System.Text.Json;

    [ApiController]
    [Route("api/auth")]
    public class VerifyController(
            IDbContextFactory<MysqlContext> contextFactory,
            RedisConnectionPool redisConnectionPool,
            ViewRenderService viewRenderService) : Controller
    {
        private readonly IDbContextFactory<MysqlContext> _contextFactory = contextFactory;
        private readonly RedisConnectionPool _redisPool = redisConnectionPool;
        private readonly ViewRenderService _viewRenderService = viewRenderService;

        private readonly string REDIS_VERIFY = "verify_";

        [HttpGet("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        { 
            IIdentity? identity = JWT.ValidateES384(token, JWT.ISSUER, Request.Host.Value);
            if (!(identity?.IsAuthenticated ?? false) || identity.Name is null) return BadRequest("Token không hợp lệ.");

            var deserializedPayload = JsonConvert.DeserializeObject<dynamic>(identity.Name);

            string UserId = deserializedPayload?.Id ?? string.Empty;
            string RedisKey = $"{REDIS_VERIFY}{UserId}";

            RedisValue[] accountRedis = Redis.HashGet(_redisPool, redisCTX => {
                RedisValue[] result = redisCTX.HashGet(RedisKey, ["Token"]);
                if (result[0].HasValue && result[0] == token) {
                    redisCTX.KeyDelete(RedisKey);
                }
                return result;
            });

            if (accountRedis[0].HasValue) {
                MysqlContext mysqlContext = _contextFactory.CreateDbContext();
                string sql = "UPDATE account Set is_email_verified = {0} WHERE id = {1}";
                Task<int> rowAffect = mysqlContext.Database.ExecuteSqlRawAsync(sql, 1, UserId);
                if(await rowAffect > 0) {
                    return Ok("Xác thực Email thành công.");
                }
                return BadRequest("Xác thực Email thất bại.");
            }
            return BadRequest("Token đã sử dụng.");
        }
    }
}
