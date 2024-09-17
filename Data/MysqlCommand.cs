using StackExchange.Redis;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using System.Collections.Generic;

namespace ACAPI.Data {
    public static class MysqlCommand {
        public static readonly string UPDATE_PHONE_BY_ID = "UPDATE account Set phone = {0} WHERE id = {1}";
        public static readonly string UPDATE_PASSWORD_BY_ID = "UPDATE account Set password = {0} WHERE id = {1}"; 
        public static readonly string UPDATE_EMAIL_BY_ID = "UPDATE account Set email = {0} WHERE id = {1}";
        public static readonly string UPDATE_PHONE_TO_NULL_BY_ID = "UPDATE account Set phone = NULL WHERE id = {0}";
    }
}
