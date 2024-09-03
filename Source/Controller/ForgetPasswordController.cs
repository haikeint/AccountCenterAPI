namespace S84Account.Controller {
    using Microsoft.AspNetCore.Mvc;
    using S84Account.Helper;
    using Microsoft.EntityFrameworkCore;
    using S84Account.Data;
    using S84Account.Model;
    using System.Security.Principal;
    using StackExchange.Redis;
    using System;
    using S84Account.Service;
    using DotNetEnv;

    [ApiController]
    [Route("api")]
    public class ForgetPasswordController(
            IDbContextFactory<MysqlContext> contextFactory,
            RedisConnectionPool redisConnectionPool,
            ViewRenderService viewRenderService) : Controller {
        private readonly IDbContextFactory<MysqlContext> _contextFactory = contextFactory;
        private readonly RedisConnectionPool _redisPool = redisConnectionPool;
        private readonly ViewRenderService _viewRenderService = viewRenderService;

        private readonly string FORGET_CODE = "ForgetCode_";

        [HttpGet("forgetpassword/username/{username}")]
        public async Task<IActionResult> CheckUser(string username, [FromQuery] string token)
        {
            if (!(await Recaptcha.Verify(token, 2))) return Ok(new {
                Error = "Recaptcha không hợp lệ"
            });
            MysqlContext mysqlContext = contextFactory.CreateDbContext();

            AccountModel? accountModel = await mysqlContext.Account
                            .Where(account => account.Username == username)
                            .Select(account => new AccountModel {
                                Email = account.Email,
                            })
                            .FirstOrDefaultAsync();

            if(accountModel is null ) return BadRequest(new {
                Error = "Tài khoản không tồn tại"
            });

            if(accountModel.Email is null) return BadRequest(new {
                Error = "Không có phương thức khôi phục tài khoản"
            });

            string jwtToken = JWT.GenerateES384(
                username,
                JWT.ISSUER, 
                Request.Host.Value,
                DateTime.UtcNow.AddMinutes(15)
                );

            return Ok(new {
                Email = MaskEmail(accountModel.Email),
                Token = jwtToken
            });
        }

        [HttpGet("forgetpassword/sendcode")]
        public async Task<IActionResult> SendCode([FromQuery] string token)
        {
            IIdentity? identity = JWT.ValidateES384(token,JWT.ISSUER, Request.Host.Value);
            if (!(identity is not null && identity.IsAuthenticated)) return Ok(new { Error = "Token hết hạn" });
            if(!(identity.Name is not null)) return Ok(new {Error = "Định dạng token không hợp lệ"});

            RedisValue[] redisResult = Redis.HashGet(_redisPool, redisCTX => {
                return redisCTX.HashGet($"{FORGET_CODE}{identity.Name}", ["Code"]);
            });
            
            if (redisResult[0].HasValue) return Ok(new { Error = "Chỉ có thể gửi mã xác nhận mỗi 60s" });
         
            MysqlContext mysqlContext = contextFactory.CreateDbContext();
            AccountModel? accountModel = await mysqlContext.Account
                .Where(account => account.Username == identity.Name)
                .Select(account => new AccountModel {
                    Email = account.Email,
                })
                .FirstOrDefaultAsync();

            if(accountModel is null ) return BadRequest(new {
                Error = "Tài khoản không tồn tại"
            });

            if(accountModel.Email is null) return BadRequest(new {
                Error = "Không có phương thức khôi phục tài khoản"
            });

            string code = Util.RandomNumber(6);

            string emailBody = await _viewRenderService.RenderToStringAsync(
                "~/Source/View/TemplateEmail.cshtml",
                new {
                    CurrentTime = DateTime.Now.ToString("dd-MM-yyyy"),
                    UrlStatic = Env.GetString("URL_STATIC"),
                    Username = identity.Name,
                    OTPCode = code
                });

            if (!Mail.Send(accountModel.Email, "Mã OTP từ HBPlay", emailBody)) {
                return Ok(new { Error = "Lỗi không xác định" });
            }
            Redis.Handle(_redisPool, redisCTX => {
                string hashKey = $"{FORGET_CODE}{identity.Name}";
                redisCTX.HashSet(hashKey, [
                    new HashEntry("Code", Util.HASH256(code)),
                    ]);
                redisCTX.KeyExpire(hashKey, TimeSpan.FromMinutes(1));
            });
            return Ok(new {
                Token = token,
            });
        }

        [HttpGet("forgetpassword/verifycode/{code}")]
        public async Task<IActionResult> VerifyCode(string code, [FromQuery] string password, [FromQuery] string token) { 
            
            IIdentity? identity = JWT.ValidateES384(token,JWT.ISSUER, Request.Host.Value);
            if (!(identity is not null && identity.IsAuthenticated)) return BadRequest(new { Error = "Token hết hạn" });
            if(!(identity.Name is not null)) return BadRequest(new {Error = "Định dạng token không hợp lệ."});

            RedisValue[] redisResult = Redis.HashGet(_redisPool, redisCTX => {
                return redisCTX.HashGet($"{FORGET_CODE}{identity.Name}", ["Code"]);
            });

            if(!redisResult[0].HasValue) return BadRequest(new {Error = "Mã OTP không tồn tại."});

            if(!(Util.HASH256(code) == redisResult[0])) {
                return BadRequest(new { Error = "Mã OTP không đúng."});
            }

            Redis.Handle(_redisPool, redisContext => {
                redisContext.KeyDelete($"{FORGET_CODE}{identity.Name}");
            });

            MysqlContext mysqlContext = _contextFactory.CreateDbContext();
            string sql = "UPDATE account Set password = {0} WHERE username = {1}";

            Task<int> rowAffect = mysqlContext.Database.ExecuteSqlRawAsync(sql, Password.Hash(password), identity.Name);

            if(await rowAffect > 0) {
                Redis.Handle(_redisPool, redisContext => {
                    redisContext.KeyDelete(identity.Name);
                });
                return Ok(new {
                    Result = "Hoàn tất"
                });
            }

            return BadRequest(new {Error = "Lỗi không xác định"});
        }

        private static string MaskEmail(string email) {
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
