﻿using S84Account.GraphQL.Middleware;
using S84Account.Model;
using S84Account.GraphQL.InputType;
using HotChocolate.Resolvers;
using Microsoft.EntityFrameworkCore;
using S84Account.Data;
using S84Account.Config;
using S84Account.Helper;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Net;

namespace S84Account.GraphQL.Mutation
{
    public class AccountMutation : ObjectTypeExtension
    {
        protected override void Configure(IObjectTypeDescriptor descriptor)
        {
            descriptor.Name("Mutation");

            descriptor.Field("updateInfo")
                .Argument("objectInput", a=> a.Type<NonNullType<UpdateInfoInputType>>())
                .Use<AuthorizedMiddleware>()
            .ResolveWith<Resolver>(res => res.UpdateInfo(default!));

            descriptor.Field("updateSecure")
                .Argument("objectInput", arg => arg.Type<NonNullType<UpdateSecureInputType>>())
                .Use<AuthorizedMiddleware>()
                .ResolveWith<Resolver>(res => res.UpdateSecure(default!));
        }

        private class Resolver(IDbContextFactory<MysqlContext> contextFactory, RedisConnectionPool redisConnectionPool) {
            private readonly IDbContextFactory<MysqlContext> _contextFactory = contextFactory;
            private readonly RedisConnectionPool _redisPool = redisConnectionPool;

            public bool UpdateInfo(IResolverContext ctx)
            {
                long UserId = long.Parse(Util.GetContextData(ctx, EnvirConst.UserId));

                UpdateInfoInput updateInfoInput = ctx.ArgumentValue<UpdateInfoInput>("objectInput");

                AccountModel accountInput = new() {
                    Id = UserId,
                    Address = updateInfoInput.Address is null ? null : updateInfoInput.Address,
                    Birthdate = updateInfoInput.Birthdate is null ? null : updateInfoInput.Birthdate,
                    Fullname = updateInfoInput.Fullname is null ? null : updateInfoInput.Fullname,
                    Gender = updateInfoInput.Gender is null ? null : updateInfoInput.Gender,
                };

                MysqlContext mysqlContext = _contextFactory.CreateDbContext();
                mysqlContext.Account.Attach(accountInput);
                foreach(PropertyEntry property in mysqlContext.Entry(accountInput).Properties) {
                    property.IsModified = property.CurrentValue is not null
                        && property.Metadata.Name != "Id";
                }
                return mysqlContext.SaveChanges() > 0;
            }

            public async Task<bool> UpdateSecure(IResolverContext ctx) {
                long UserId = long.Parse(Util.GetContextData(ctx, EnvirConst.UserId));

                UpdateSecureInput updateInput = ctx.ArgumentValue<UpdateSecureInput>("objectInput");

                MysqlContext mysqlContext = _contextFactory.CreateDbContext();
                AccountModel? accountModel = await mysqlContext.Account
                    .Where(account => account.Id == UserId)
                    .Select(account => new AccountModel {
                        Username = account.Username,
                        Password = updateInput.OldPassword != null ? account.Password : null,
                        Phone = updateInput.OldPhone != null ? account.Phone : null,
                        Email = updateInput.OldEmail != null ? account.Email : null
                    })
                    .FirstOrDefaultAsync() ?? throw Util.Exception(HttpStatusCode.NotFound);

                AccountModel accountUpdate = new () {
                    Id = UserId
                };

                if(updateInput.OldPassword is not null) {
                    accountUpdate.Password = HandlePassword(
                        updateInput.OldPassword, 
                        accountModel.Password,
                        updateInput.NewPassword 
                    );
                }

                if((accountModel.Email is null && updateInput.NewEmail is not null)
                    || (accountModel.Email is not null && accountModel.Email == updateInput.OldEmail)) {
                    accountUpdate.Email = updateInput.NewEmail;
                } 

                if ((accountModel.Phone is null && updateInput.NewPhone is not null) 
                    || (accountModel.Phone is not null && accountModel.Phone == updateInput.OldPhone)) { 
                    accountUpdate.Phone = updateInput.NewPhone;
                }

                mysqlContext.Account.Attach(accountUpdate);

                foreach(PropertyEntry property in mysqlContext.Entry(accountUpdate).Properties) {
                    property.IsModified = property.CurrentValue is not null
                        && property.Metadata.Name != "Id";
                }

                int rowAffect = mysqlContext.SaveChanges();

                if(rowAffect > 0) PurgeRedis(accountModel.Username);

                return rowAffect > 0;
            }

            private static string HandlePassword(string? oldPassword, string? hashPassword, string? newPassword) {
                if(newPassword is null) throw Util.Exception(HttpStatusCode.Unauthorized);
                if(!Password.Verify(oldPassword, hashPassword)) throw Util.Exception(HttpStatusCode.Unauthorized);

                return Password.Hash(newPassword);
            }

            private void PurgeRedis(string? username) {
                if(username is null) return;

                Redis.Handle(_redisPool, redisContext => {
                    redisContext.KeyDelete(username);
                });
            }
        }
    }
}
