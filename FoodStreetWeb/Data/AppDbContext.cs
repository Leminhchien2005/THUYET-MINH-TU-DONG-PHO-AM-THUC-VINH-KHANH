using Microsoft.EntityFrameworkCore;
using FoodStreetWeb.Models;

namespace FoodStreetWeb.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Poi> Pois { get; set; }
    }
}