using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using S84Account.Model;

namespace S84Account.Data
{
    public class LibraryContext(DbContextOptions<LibraryContext> options) : DbContext(options)
    {
        public DbSet<AccountModel> Account { get; set; }
    }
}
