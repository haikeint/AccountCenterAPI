using Microsoft.EntityFrameworkCore;
using S84Account.Model;
using Microsoft.Extensions.Options;

namespace S84Account.Data
{
    public class LibraryContext(DbContextOptions<LibraryContext> options) : DbContext(options)
    {
        public DbSet<AccountModel> Account { get; set; }
    }
}
