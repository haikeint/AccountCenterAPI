using Microsoft.EntityFrameworkCore;
using ACAPI.Data;
using ACAPI.Service;
using ACAPI.Helper;
using Newtonsoft.Json;
using DotNetEnv;

namespace ACAPI.Controller {

    using Microsoft.AspNetCore.Mvc;
    using StackExchange.Redis;
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
            string Email = deserializedPayload?.Email ?? string.Empty;
            string NewEmail = deserializedPayload?.NewEmail ?? string.Empty;
            string Operator = deserializedPayload?.Operator ?? string.Empty;
            
            string RedisKey = $"{REDIS_VERIFY}{UserId}";

            RedisValue[] accountRedis = Redis.GetValue(_redisPool, redisCTX => {
                RedisValue[] result = redisCTX.HashGet(RedisKey, ["Token"]);
                if (result[0].HasValue && result[0] == token) {
                    redisCTX.KeyDelete(RedisKey);
                }
                return result;
            });

            if (accountRedis[0].HasValue) {
                MysqlContext mysqlContext = _contextFactory.CreateDbContext();
                if(!string.IsNullOrEmpty(Operator) && Operator == "RequestChangeEmail") {
                    if(await SendVerifyEmail(Request.Host.Value, NewEmail, UserId, UserId)) {
                         string sql = "UPDATE account Set email = {0}, is_email_verified = 0 WHERE id = {1} and email = {2}";
                        Task<int> rowAffect = mysqlContext.Database.ExecuteSqlRawAsync(sql, NewEmail, UserId, Email);
                        if(!(await rowAffect > 0)) return BadRequest("Lỗi không xác định");
                        return Ok($"Vui lòng kiểm tra hòm thư {NewEmail} và xác thực email.");
                    }
                    return BadRequest("Lỗi không xác định");
                } else { 
                    string sql = "UPDATE account Set is_email_verified = {0} WHERE id = {1}";
                    Task<int> rowAffect = mysqlContext.Database.ExecuteSqlRawAsync(sql, 1, UserId);

                    if(!(await rowAffect > 0)) return BadRequest("Lỗi không xác định");
                    return Ok("Xác thực Email thành công.");
                } 
            }
            return BadRequest("Token đã sử dụng.");
        }

        private async Task<bool> SendVerifyEmail(string host, string email, string userId, string username)
        { 
                string redisKey = $"{REDIS_VERIFY}{userId}";

                TimeSpan? ttl = Redis.GetValue(_redisPool, redisCTX => redisCTX.KeyTimeToLive(redisKey));

                if(ttl is not null) return false;
                
                var payload = new {
                    Id = userId,
                    Email= email,
                    Operator = "VerifyEmail"
                };
               
                string jwtToken = JWT.GenerateES384(
                    JsonSerializer.Serialize(payload),
                    JWT.ISSUER,
                    host, 
                    DateTime.UtcNow.AddMinutes(30));

                Redis.Handle(_redisPool, redisCTX =>
                {
                    redisCTX.HashSet(redisKey, [
                        new HashEntry("Token", jwtToken),
                        ]);
                    redisCTX.KeyExpire(redisKey, TimeSpan.FromMinutes(30));
                });

                string verifyLink = $"https://localhost:5000/api/auth/verify-email?token={jwtToken}";

                string emailBody = await _viewRenderService.RenderToStringAsync(
                    "~/View/VerifyEmailTempalte.cshtml",
                    new {
                        UrlStatic = Env.GetString("URL_STATIC"),
                        Username = username,
                        Text = "xác thực Email",
                        ButtonHTML = "Xác thực Email ngay",
                        Expire = 30,
                        VerifyLink = verifyLink
                    });
                return Mail.Send(email ?? string.Empty, "Xác thực Email tại HBPlay", emailBody);
        }
    }
}
