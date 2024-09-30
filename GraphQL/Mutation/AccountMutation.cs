using ACAPI.GraphQL.Middleware;
using ACAPI.Model;
using ACAPI.GraphQL.InputType;
using HotChocolate.Resolvers;
using Microsoft.EntityFrameworkCore;
using ACAPI.Data;
using ACAPI.Config;
using ACAPI.Helper;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Net;
using ACAPI.Service;
using DotNetEnv;
using StackExchange.Redis;
using System.Text.Json;

namespace ACAPI.GraphQL.Mutation
{
    public class AccountMutation : ObjectTypeExtension
    {
        protected override void Configure(IObjectTypeDescriptor descriptor)
        {
            descriptor.Name("Mutation");

            descriptor.Field("updateInfo")
                .Argument("objectInput", a => a.Type<NonNullType<UpdateInfoInputType>>())
                .Use<AuthorizedMiddleware>()
            .ResolveWith<Resolver>(res => res.UpdateInfo(default!));

            //descriptor.Field("updateSecure")
            //    .Argument("objectInput", arg => arg.Type<NonNullType<UpdateSecureInputType>>())
            //    .Use<AuthorizedMiddleware>()
            //    .ResolveWith<Resolver>(res => res.UpdateSecure(default!));

            descriptor.Field("sendVerifyEmail")
                .Use<AuthorizedMiddleware>()
                .ResolveWith<Resolver>(res => res.SendVerifyEmail(default!));

            descriptor.Field("changePassword")
                .Argument("oldPassword", arg => arg.Type<NonNullType<StringType>>())
                .Argument("newPassword", arg => arg.Type<NonNullType<StringType>>())
                .Use<AuthorizedMiddleware>()
                .ResolveWith<Resolver>(res => res.ChangePassword(default!));

            descriptor.Field("addPhoneNumber")
                .Argument("newPhone", arg => arg.Type<NonNullType<StringType>>())
                .Use<AuthorizedMiddleware>()
                .ResolveWith<Resolver>(res => res.AddPhoneNumber(default!));

            descriptor.Field("verifyAddPhoneNumber")
                .Argument("newPhone", arg => arg.Type<NonNullType<StringType>>())
                .Argument("otp", arg => arg.Type<NonNullType<StringType>>())
                .Use<AuthorizedMiddleware>()
                .ResolveWith<Resolver>(res => res.VerifyAddPhoneNumber(default!));

            descriptor.Field("deletePhoneNumber")
                .Argument("oldPhone", arg => arg.Type<NonNullType<StringType>>())
                .Use<AuthorizedMiddleware>()
                .ResolveWith<Resolver>(res => res.DeletePhoneNumber(default!));

            descriptor.Field("verifyDeletePhoneNumber")
                .Argument("oldPhone", arg => arg.Type<NonNullType<StringType>>())
                .Argument("otp", arg => arg.Type<NonNullType<StringType>>())
                .Use<AuthorizedMiddleware>()
                .ResolveWith<Resolver>(res => res.VerifyDeletePhoneNumber(default!));

            descriptor.Field("changeEmail")
                .Argument("oldEmail", arg => arg.Type<StringType>())
                .Argument("newEmail", arg => arg.Type<NonNullType<StringType>>())
                .Use<AuthorizedMiddleware>()
                .ResolveWith<Resolver>(res => res.ChangeEmail(default!));
        }

        private class Resolver(
            IDbContextFactory<MysqlContext> contextFactory, 
            IConnectionMultiplexer redis,
            ViewRenderService viewRenderService)
        {
            private readonly IDbContextFactory<MysqlContext> _contextFactory = contextFactory;
            private readonly IConnectionMultiplexer _redis = redis;

            private readonly ViewRenderService _viewRenderService = viewRenderService;

            private readonly string REDIS_VERIFY = "verify_";
            private readonly string REDIS_VERIFY_PHONE = "verifyPhone_";

            public async Task<string> ChangePassword(IResolverContext ctx) { 
                string oldPassword = ctx.ArgumentValue<string>("oldPassword");
                string newPassword = ctx.ArgumentValue<string>("newPassword");

                long UserId = long.Parse(Util.GetContextData(ctx, EnvirConst.UserId));
                MysqlContext mysqlContext = _contextFactory.CreateDbContext();

                AccountModel? accountModel = await mysqlContext.Account
                    .Where(account => account.Id == UserId)
                    .Select(account => new AccountModel
                    {
                        Username = account.Username,
                        Password = account.Password,
                    })
                    .FirstOrDefaultAsync() ?? throw Util.Exception(HttpStatusCode.Forbidden, "Tài khoản không tồn tại.");
                
                if(!Password.Verify(oldPassword, accountModel.Password)) {
                    throw Util.Exception(HttpStatusCode.Forbidden, "Mật khẩu hiện tại không đúng.");
                }

                Task<int> rowAffect = mysqlContext.Database.ExecuteSqlRawAsync(
                    MysqlCommand.UPDATE_PASSWORD_BY_ID, 
                    Password.Hash(newPassword), 
                    UserId);
                if(!(await rowAffect > 0)) throw Util.Exception(HttpStatusCode.Forbidden, "Lỗi không xác định");

                PurgeRedis(accountModel.Username);
                return "Thay đổi mật khẩu thành công";
            } 
            public string AddPhoneNumber(IResolverContext ctx) { 
                string newPhone = ctx.ArgumentValue<string>("newPhone");
                if(newPhone.Length != 10 || !ValidatePhoneNumber(newPhone)) {
                    throw Util.Exception(HttpStatusCode.Forbidden, "Số điện thoại di động Việt Nam không hợp lệ");
                } 
                
                long UserId = long.Parse(Util.GetContextData(ctx, EnvirConst.UserId));
                string code = Util.RandomNumber(6);
                string redisKey = $"{REDIS_VERIFY_PHONE}_{UserId}_{newPhone}";
                
                IDatabase redisDB = _redis.GetDatabase();
                TimeSpan? ttl = null;
                RedisValue[] expire = redisDB.HashGet(redisKey, ["EXPIRE"]);
                if (expire[0].HasValue) ttl = GetOTPExpire((long)expire[0]);

                if(ttl is null) {
                    redisDB.HashSet(redisKey, [
                        new HashEntry("OTP", code),
                        new HashEntry("EXPIRE", DateTimeOffset.UtcNow.AddMinutes(2).ToUnixTimeSeconds())
                    ]);
                    redisDB.KeyExpire(redisKey, TimeSpan.FromMinutes(10));
                }
         
                if(ttl is not null) throw Util.Exception(HttpStatusCode.Forbidden, $"Gửi lại mã OTP sau {ttl.Value.TotalSeconds}s"); 
                if(!SMS.Send(newPhone, $"Mã xác thực của bạn là: {code}")) {
                    throw Util.Exception(HttpStatusCode.InternalServerError, "Lỗi gửi mã OTP vào điện thoại");
                }
                return "Đã gửi mã OTP vào số điện thoại.";
            }

            public async Task<string> VerifyAddPhoneNumber(IResolverContext ctx) { 
       
                string newPhone = ctx.ArgumentValue<string>("newPhone");
                string otp = ctx.ArgumentValue<string>("otp");

                if(newPhone.Length != 10 || !ValidatePhoneNumber(newPhone)) {
                    throw Util.Exception(HttpStatusCode.Forbidden, "Số điện thoại di động Việt Nam không hợp lệ");
                } 
                
                long UserId = long.Parse(Util.GetContextData(ctx, EnvirConst.UserId));
                string redisKey = $"{REDIS_VERIFY_PHONE}_{UserId}_{newPhone}";

                IDatabase redisDB = _redis.GetDatabase();
                RedisValue[] redisResult = redisDB.HashGet(redisKey, ["OTP", "EXPIRE"]);

                if (!redisResult[0].HasValue && !redisResult[1].HasValue) { 
                    throw Util.Exception(HttpStatusCode.NotFound, "Mã OTP không tồn tại.");
                }
                if(redisResult[0] != otp) throw Util.Exception(HttpStatusCode.Forbidden, "Mã OTP không đúng.");

                if(DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= (long)redisResult[1]) {
                    throw Util.Exception(HttpStatusCode.Forbidden, "Mã OTP hết hạn");
                }
                redisDB.KeyDelete(redisKey);

                MysqlContext mysqlContext = _contextFactory.CreateDbContext();

                AccountModel? accountModel = await mysqlContext.Account
                    .Where(account => account.Id == UserId)
                    .Select(account => new AccountModel {
                        Username = account.Username,
                        Phone = account.Phone,
                    })
                    .FirstOrDefaultAsync() ?? throw Util.Exception(HttpStatusCode.Forbidden, "Tài khoản không tồn tại.");

                Task<int> rowAffect = mysqlContext.Database.ExecuteSqlRawAsync(
                    MysqlCommand.UPDATE_PHONE_BY_ID,
                    newPhone,
                    UserId);
                if (!(await rowAffect > 0)) throw Util.Exception(HttpStatusCode.Forbidden, "Lỗi không xác định");

                PurgeRedis(accountModel.Username);
                return "Đã thay đổi số điện thoại thành công.";
            }
            public async Task<string> DeletePhoneNumber(IResolverContext ctx) { 
                string oldPhone = ctx.ArgumentValue<string>("oldPhone");

                if(oldPhone.Length != 10 || !ValidatePhoneNumber(oldPhone)) {
                    throw Util.Exception(HttpStatusCode.Forbidden, "Số điện thoại di động Việt Nam không hợp lệ");
                } 

                long UserId = long.Parse(Util.GetContextData(ctx, EnvirConst.UserId));
                string code = Util.RandomNumber(6);
                string redisKey = $"{REDIS_VERIFY_PHONE}_{UserId}_{oldPhone}";
           
                IDatabase redisDB = _redis.GetDatabase();
                TimeSpan? ttl = null;
                RedisValue[] expire = redisDB.HashGet(redisKey, ["EXPIRE"]);
                if (expire[0].HasValue) ttl = GetOTPExpire((long)expire[0]);
                //return ttl;

                //TimeSpan? ttl = Redis.GetValue(_redisPool, redisCTX =>
                //{
                //    TimeSpan? ttl = null;
                //    RedisValue[] expire = redisCTX.HashGet(redisKey, ["EXPIRE"]);
                //    if (expire[0].HasValue) ttl = GetOTPExpire((long)expire[0]);
                //    return ttl;
                //});
                
                if(ttl is not null) throw Util.Exception(HttpStatusCode.Forbidden, $"Gửi lại mã OTP sau {ttl.Value.TotalSeconds}s"); 
                
                MysqlContext mysqlContext = _contextFactory.CreateDbContext();
                AccountModel? accountModel = await mysqlContext.Account
                    .Where(account => account.Id == UserId)
                    .Select(account => new AccountModel {
                        Username = account.Username,
                        Phone = account.Phone,
                    })
                    .FirstOrDefaultAsync() ?? throw Util.Exception(HttpStatusCode.Forbidden, "Tài khoản không tồn tại.");
                if(accountModel.Phone is not null && oldPhone != accountModel.Phone) {
                    throw Util.Exception(HttpStatusCode.Forbidden, "Số điện thoại hiện tại không đúng");
                }

                if(!SMS.Send(oldPhone, $"Mã xác thực của bạn là: {code}")) {
                    throw Util.Exception(HttpStatusCode.InternalServerError, "Lỗi gửi mã OTP vào điện thoại");
                }
                
                redisDB.HashSet(redisKey, [
                    new HashEntry("OTP", code),
                    new HashEntry("EXPIRE", DateTimeOffset.UtcNow.AddMinutes(2).ToUnixTimeSeconds())
                ]);
                bool result = redisDB.KeyExpire(redisKey, TimeSpan.FromMinutes(10));
               
                if(!result) {
                    throw Util.Exception(HttpStatusCode.InternalServerError, "Lỗi gửi mã OTP vào điện thoại");
                }
                return "Đã gửi mã OTP vào số điện thoại hiện tại";
            }
            
            public async Task<string> VerifyDeletePhoneNumber(IResolverContext ctx) { 
                string oldPhone = ctx.ArgumentValue<string>("oldPhone");
                string otp = ctx.ArgumentValue<string>("otp");

                if(oldPhone.Length != 10 || !ValidatePhoneNumber(oldPhone)) {
                    throw Util.Exception(HttpStatusCode.Forbidden, "Số điện thoại di động Việt Nam không hợp lệ");
                } 
                
                long UserId = long.Parse(Util.GetContextData(ctx, EnvirConst.UserId));
                string redisKey = $"{REDIS_VERIFY_PHONE}_{UserId}_{oldPhone}";

                IDatabase redisDB = _redis.GetDatabase();
                RedisValue[] redisResult = redisDB.HashGet(redisKey, ["OTP"]);

                if (!redisResult[0].HasValue) throw Util.Exception(HttpStatusCode.NotFound, "Mã OTP không tồn tại.");

                if(redisResult[0] != otp) throw Util.Exception(HttpStatusCode.Forbidden, "Mã OTP không đúng.");

                redisDB.KeyDelete(redisKey);

                MysqlContext mysqlContext = _contextFactory.CreateDbContext();

                AccountModel? accountModel = await mysqlContext.Account
                    .Where(account => account.Id == UserId)
                    .Select(account => new AccountModel {
                        Username = account.Username,
                        Phone = account.Phone,
                    })
                    .FirstOrDefaultAsync() ?? throw Util.Exception(HttpStatusCode.Forbidden, "Tài khoản không tồn tại.");

                Task<int> rowAffect = mysqlContext.Database.ExecuteSqlRawAsync(
                    MysqlCommand.UPDATE_PHONE_TO_NULL_BY_ID,
                    UserId);
                if (!(await rowAffect > 0)) throw Util.Exception(HttpStatusCode.Forbidden, "Lỗi không xác định");

                PurgeRedis(accountModel.Username);
                return "Đã xóa số điện thoại";
            }
            public async Task<string> ChangeEmail(IResolverContext ctx) { 
                string responeMessage = "";

                string oldEmail = ctx.ArgumentValue<string>("oldEmail");
                string newEmail = ctx.ArgumentValue<string>("newEmail");
                if(string.IsNullOrWhiteSpace(newEmail)) {
                    throw Util.Exception(HttpStatusCode.Forbidden, "Email không hợp lệ");
                }
                
                long UserId = long.Parse(Util.GetContextData(ctx, EnvirConst.UserId));

                MysqlContext mysqlContext = _contextFactory.CreateDbContext();

                AccountModel? accountModel = await mysqlContext.Account
                    .Where(account => account.Id == UserId)
                    .Select(account => new AccountModel
                    {
                        Email = account.Email,
                        Username = account.Username,
                        IsEmailVerified = account.IsEmailVerified,
                    })
                    .FirstOrDefaultAsync() ?? throw Util.Exception(HttpStatusCode.Forbidden, "Tài khoản không tồn tại.");
                
                if (string.IsNullOrWhiteSpace(accountModel.Email) 
                    || (!string.IsNullOrWhiteSpace(accountModel.Email) 
                    && accountModel.IsEmailVerified == false)) {

                        Task<int> rowAffect = mysqlContext.Database.ExecuteSqlRawAsync(
                            MysqlCommand.UPDATE_EMAIL_BY_ID, 
                            newEmail, 
                            UserId);
                        if(!(await rowAffect > 0)) throw Util.Exception(HttpStatusCode.Forbidden, "Lỗi không xác định");

                        PurgeRedis(accountModel.Username);
                        responeMessage = await SendVerifyEmail(ctx) 
                            ? "Vui lòng vào Email xác thực Email mới" 
                            : "Đã thay đổi Email nhưng chưa xác thực.";
                }

                if(!string.IsNullOrWhiteSpace(accountModel.Email) 
                    && accountModel.IsEmailVerified == true) {
                    if(accountModel.Email !=  oldEmail) throw Util.Exception(HttpStatusCode.Forbidden, "Email hiện tại không đúng.");
                    bool result = await SendMailForChangeEmail(
                        UserId, 
                        accountModel.Username ?? string.Empty, 
                        accountModel.Email, 
                        newEmail);
                    responeMessage = "Kiểm tra hòm thư Email để xác nhận việc thay đổi Email";
                }

                return responeMessage;
            }
            public async Task<bool> SendVerifyEmail(IResolverContext ctx) {
                long UserId = long.Parse(Util.GetContextData(ctx, EnvirConst.UserId));

                string redisKey = $"{REDIS_VERIFY}{UserId}";
                IDatabase redisDB = _redis.GetDatabase();

                TimeSpan? ttl = redisDB.KeyTimeToLive(redisKey);

                if(ttl is not null) {
                    throw Util.Exception(HttpStatusCode.Forbidden, $"Gửi lại mã xác thực sau {ttl.Value.Minutes} phút");
                }

                MysqlContext mysqlContext = _contextFactory.CreateDbContext();
                AccountModel accountModel = await mysqlContext.Account
                    .Where(acc => acc.Id == UserId)
                    .Select(acc => new AccountModel {
                        Email = acc.Email,
                        Username = acc.Username,
                    }).FirstOrDefaultAsync() ?? throw Util.Exception(HttpStatusCode.NotFound); 
                
                if(accountModel.Email is null) {
                    throw Util.Exception(HttpStatusCode.Forbidden, "Tài khoản chưa có Email"); 
                }

                if(accountModel.IsEmailVerified is not null && (bool)accountModel.IsEmailVerified) {
                    throw Util.Exception(HttpStatusCode.Forbidden, "Tài khoản đã xác thực Email");
                }
                
                var payload = new {
                    Id = UserId,
                    Email= accountModel.Email ?? string.Empty,
                    Operator = "VerifyEmail"
                };
                string host = Env.GetString("HOST");
                string jwtToken = JWT.GenerateES384(
                    JsonSerializer.Serialize(payload),
                    JWT.ISSUER,
                    host, 
                    DateTime.UtcNow.AddMinutes(30));

                redisDB.HashSet(redisKey, [
                    new HashEntry("Token", jwtToken),
                ]);
                redisDB.KeyExpire(redisKey, TimeSpan.FromMinutes(30));

                string verifyLink = $"https://{host}/api/auth/verify-email?token={jwtToken}";

                string emailBody = await _viewRenderService.RenderToStringAsync(
                    "~/View/VerifyEmailTempalte.cshtml",
                    new {
                        UrlStatic = Env.GetString("URL_STATIC"),
                        Username = accountModel.Username ?? string.Empty,
                        Text = "xác thực Email",
                        ButtonHTML = "Xác thực Email ngay",
                        Expire = 30,
                        VerifyLink = verifyLink
                    });
                return Mail.Send(accountModel.Email ?? string.Empty, "Xác thực Email tại HBPlay", emailBody);
            }

            public bool UpdateInfo(IResolverContext ctx)
            {
                long UserId = long.Parse(Util.GetContextData(ctx, EnvirConst.UserId));

                UpdateInfoInput updateInfoInput = ctx.ArgumentValue<UpdateInfoInput>("objectInput");

                AccountModel accountInput = new()
                {
                    Id = UserId,
                    Address = updateInfoInput.Address is null ? null : updateInfoInput.Address,
                    Birthdate = updateInfoInput.Birthdate is null ? null : updateInfoInput.Birthdate,
                    Fullname = updateInfoInput.Fullname is null ? null : updateInfoInput.Fullname,
                    Gender = updateInfoInput.Gender is null ? null : updateInfoInput.Gender,
                };

                MysqlContext mysqlContext = _contextFactory.CreateDbContext();
                mysqlContext.Account.Attach(accountInput);
                foreach (PropertyEntry property in mysqlContext.Entry(accountInput).Properties)
                {
                    property.IsModified = property.CurrentValue is not null
                        && property.Metadata.Name != "Id";
                }
                return mysqlContext.SaveChanges() > 0;
            }

            private void PurgeRedis(string? username)
            {
                if (username is null) return;
                IDatabase redisDB = _redis.GetDatabase();
                redisDB.KeyDelete(username);
            }

            private async Task<bool> SendMailForChangeEmail(long UserId, string Username ,string oldEmail, string newEmail) {

                string redisKey = $"{REDIS_VERIFY}{UserId}";
                IDatabase redisDB = _redis.GetDatabase();
                TimeSpan? ttl = redisDB.KeyTimeToLive(redisKey);

                if(ttl is not null) {
                    throw Util.Exception(HttpStatusCode.Forbidden, $"Thực hiện lại thao tác sau {ttl.Value.Minutes} phút");
                }

                var payload = new {
                    Id = UserId,
                    Email= oldEmail,
                    NewEmail = newEmail,
                    Operator = "RequestChangeEmail"
                };
                
                string host = Env.GetString("HOST");
                string jwtToken = JWT.GenerateES384(
                    JsonSerializer.Serialize(payload),
                    JWT.ISSUER,
                    host, 
                    DateTime.UtcNow.AddMinutes(30));

                redisDB.HashSet(redisKey, [
                    new HashEntry("Token", jwtToken),
                ]);
                redisDB.KeyExpire(redisKey, TimeSpan.FromMinutes(30));

                string verifyLink = $"https://{host}/api/auth/verify-email?token={jwtToken}";

                string emailBody = await _viewRenderService.RenderToStringAsync(
                    "~/View/VerifyEmailTempalte.cshtml",
                    new {
                        UrlStatic = Env.GetString("URL_STATIC"),
                        Username = Username ?? string.Empty,
                        Text = "thay đổi Email",
                        ButtonHTML = "Thay đổi Email ngay",
                        Expire = 30,
                        VerifyLink = verifyLink
                    });
                return Mail.Send(oldEmail ?? string.Empty, "Yêu cầu thay đổi Email tại HBPlay", emailBody);
            }

            private static readonly List<string> PHONE_NUMBER_VALID = [];
            private bool ValidatePhoneNumber(string phoneNumber) {
                if(PHONE_NUMBER_VALID.Count.Equals(0)) {
                    MysqlContext mysqlContext = _contextFactory.CreateDbContext();
                    IEnumerable<PhonePrefixModel> phonePrefixModel = mysqlContext.PhonePrefix
                        .Select(prefix => new PhonePrefixModel {
                            Prefix = prefix.Prefix
                        });
                    foreach (PhonePrefixModel item in phonePrefixModel.ToList()) {
                        if (item.Prefix is not null) PHONE_NUMBER_VALID.Add(item.Prefix);
                    }
                }
                return PHONE_NUMBER_VALID.Contains(phoneNumber[..3]);
            }

            private static TimeSpan? GetOTPExpire(long expire) {
                TimeSpan? ttl = null;
                long unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (expire >= unixTimestamp) {

                    DateTimeOffset currentTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);
                    DateTimeOffset futureTime = DateTimeOffset.FromUnixTimeSeconds(expire);
                    ttl  = futureTime - currentTime;
                }
                return ttl;
            }
        }
    }
}
