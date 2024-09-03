using Microsoft.EntityFrameworkCore;
using ACAPI.Model;

namespace ACAPI.Data
{
    public class MysqlContext(DbContextOptions<MysqlContext> options) : DbContext(options)
    {
        public DbSet<AccountModel> Account { get; set; }
    }
}
