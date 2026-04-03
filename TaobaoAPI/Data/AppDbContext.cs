using Microsoft.EntityFrameworkCore;
using TaobaoAPI.Models;
namespace TaobaoAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        public DbSet<OrderDto> Orders { get; set; }
    }
}
