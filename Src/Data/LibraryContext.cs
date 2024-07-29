using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using S84Account.Src.Model;

namespace S84Account.Src.Data
{
    public class LibraryContext(DbContextOptions<LibraryContext> options) : DbContext(options)
    {
        public DbSet<AccountModel> Account { get; set; }
    }
}
