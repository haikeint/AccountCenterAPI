using ACAPI.Helper;
using Microsoft.EntityFrameworkCore;
using ACAPI.Data;
using ACAPI.Model;
using System.Security.Principal;
using StackExchange.Redis;
using ACAPI.Service;
using DotNetEnv;

namespace ACAPI.Controller
{
    using Microsoft.AspNetCore.Mvc;
    
    [ApiController]
    [Route("api/forgetpassword")]
    public class ForgetPasswordController(
            IDbContextFactory<MysqlContext> contextFactory,
            RedisConnectionPool redisConnectionPool,
            ViewRenderService viewRenderService) : Controller
    {
        private readonly IDbContextFactory<MysqlContext> _contextFactory = contextFactory;
        private readonly RedisConnectionPool _redisPool = redisConnectionPool;
        private readonly ViewRenderService _viewRenderService = viewRenderService;

        private readonly string FORGET_CODE = "ForgetCode_";

        [HttpGet("username/{username}")]
        public async Task<IActionResult> CheckUser(string username, [FromQuery] string token)
        {
            if (!(await Recaptcha.Verify(token, 2))) return Ok(new
            {
                Error = "Recaptcha không hợp lệ"
            });
            MysqlContext mysqlContext = contextFactory.CreateDbContext();

            AccountModel? accountModel = await mysqlContext.Account
                            .Where(account => account.Username == username)
                            .Select(account => new AccountModel
                            {
                                Email = account.Email,
                            })
                            .FirstOrDefaultAsync();

            if (accountModel is null) return BadRequest(new
            {
                Error = "Tài khoản không tồn tại"
            });

            if (accountModel.Email is null) return BadRequest(new
            {
                Error = "Không có phương thức khôi phục tài khoản"
            });

            string jwtToken = JWT.GenerateES384(
                username,
                JWT.ISSUER,
                Request.Host.Value,
                DateTime.UtcNow.AddMinutes(15)
                );

            return Ok(new
            {
                Email = MaskEmail(accountModel.Email),
                Token = jwtToken
            });
        }

        [HttpGet("sendcode-to-email")]
        public async Task<IActionResult> SendCodeToEmail([FromQuery] string token)
        {
            IIdentity? identity = JWT.ValidateES384(token, JWT.ISSUER, Request.Host.Value);
            if (!(identity is not null && identity.IsAuthenticated)) return Ok(new { Error = "Token hết hạn" });
            if (!(identity.Name is not null)) return Ok(new { Error = "Định dạng token không hợp lệ" });

            RedisValue[] redisResult = Redis.HashGet(_redisPool, redisCTX =>
            {
                return redisCTX.HashGet($"{FORGET_CODE}{identity.Name}", ["Code"]);
            });

            if (redisResult[0].HasValue) return Ok(new { Error = "Chỉ có thể gửi mã xác nhận mỗi 60s" });

            MysqlContext mysqlContext = contextFactory.CreateDbContext();
            AccountModel? accountModel = await mysqlContext.Account
                .Where(account => account.Username == identity.Name)
                .Select(account => new AccountModel
                {
                    Email = account.Email,
                })
                .FirstOrDefaultAsync();

            if (accountModel is null) return BadRequest(new
            {
                Error = "Tài khoản không tồn tại"
            });

            if (accountModel.Email is null) return BadRequest(new
            {
                Error = "Không có phương thức khôi phục tài khoản"
            });

            string code = Util.RandomNumber(6);

            string emailBody = await _viewRenderService.RenderToStringAsync(
                "~/View/SendCodeEmailTemplate.cshtml",
                new
                {
                    CurrentTime = DateTime.Now.ToString("dd-MM-yyyy"),
                    UrlStatic = Env.GetString("URL_STATIC"),
                    Operation = "khôi phục mật khẩu",
                    Username = identity.Name,
                    Expire = 1,
                    OTPCode = code
                });

            if (!Mail.Send(accountModel.Email, "Mã OTP từ HBPlay", emailBody))
            {
                return Ok(new { Error = "Lỗi không xác định" });
            }
            Redis.Handle(_redisPool, redisCTX =>
            {
                string hashKey = $"{FORGET_CODE}{identity.Name}";
                redisCTX.HashSet(hashKey, [
                    new HashEntry("Code", Util.HASH256(code)),
                    ]);
                redisCTX.KeyExpire(hashKey, TimeSpan.FromMinutes(1));
            });
            return Ok(new
            {
                Token = token,
            });
        }

        [HttpGet("verifycode-from-email/{code}")]
        public async Task<IActionResult> VerifyCodeFromEmail(string code, [FromQuery] string password, [FromQuery] string token)
        {

            IIdentity? identity = JWT.ValidateES384(token, JWT.ISSUER, Request.Host.Value);
            if (!(identity is not null && identity.IsAuthenticated)) return BadRequest(new { Error = "Token hết hạn" });
            if (!(identity.Name is not null)) return BadRequest(new { Error = "Định dạng token không hợp lệ." });

            RedisValue[] redisResult = Redis.HashGet(_redisPool, redisCTX =>
            {
                return redisCTX.HashGet($"{FORGET_CODE}{identity.Name}", ["Code"]);
            });

            if (!redisResult[0].HasValue) return BadRequest(new { Error = "Mã OTP không tồn tại." });

            if (!(Util.HASH256(code) == redisResult[0]))
            {
                return BadRequest(new { Error = "Mã OTP không đúng." });
            }

            Redis.Handle(_redisPool, redisContext =>
            {
                redisContext.KeyDelete($"{FORGET_CODE}{identity.Name}");
            });

            MysqlContext mysqlContext = _contextFactory.CreateDbContext();
            string sql = "UPDATE account Set password = {0} WHERE username = {1}";

            Task<int> rowAffect = mysqlContext.Database.ExecuteSqlRawAsync(sql, Password.Hash(password), identity.Name);

            if (await rowAffect > 0)
            {
                Redis.Handle(_redisPool, redisContext =>
                {
                    redisContext.KeyDelete(identity.Name);
                });
                return Ok(new
                {
                    Result = "Hoàn tất"
                });
            }

            return BadRequest(new { Error = "Lỗi không xác định" });
        }

        private static string MaskEmail(string email)
        {
            int atIndex = email.IndexOf('@');
            int numberOfmask = 6;
            if (atIndex <= 1)
            {
                return email;
            }

            string name = email[..atIndex];
            string domain = email[atIndex..];

            string maskedName = name[0..3] + new string('*', numberOfmask);
            return maskedName + domain;
        }
    }
}
