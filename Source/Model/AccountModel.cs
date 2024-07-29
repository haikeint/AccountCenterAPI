using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;

namespace S84Account.Model
{
    public class AccountModel
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Phone { get; set; }
        public string Address { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Idcode { get; set; } = string.Empty;
    }
}
