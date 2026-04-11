using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using FoodStreetWeb.Models;

namespace FoodStreetWeb.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Poi> Pois { get; set; }

        public DbSet<PoiRequest> PoiRequests { get; set; }

        public DbSet<Food> Foods { get; set; }

        public DbSet<FoodRequest> FoodRequests { get; set; }

        public DbSet<PoiTranslation> PoiTranslations { get; set; }
        public DbSet<FoodTranslation> FoodTranslations { get; set; }

        public DbSet<QRCodeEntity> QRCodes { get; set; }
    }
}