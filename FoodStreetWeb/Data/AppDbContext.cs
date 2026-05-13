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

        public DbSet<ScanLog> ScanLogs { get; set; }
        public DbSet<NarrationLog> NarrationLogs { get; set; }
        public DbSet<OnlineWebPresence> OnlineWebPresences { get; set; }
        public DbSet<DeviceConnectionHistory> DeviceConnectionHistories { get; set; }

        public DbSet<AudioTranslation> AudioTranslations { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Tối ưu truy vấn dashboard theo thời gian + nhà hàng.
            builder.Entity<ScanLog>()
                .HasIndex(x => new { x.RestaurantId, x.ScanTime });

            builder.Entity<ScanLog>()
                .HasIndex(x => x.ScanTime);

            builder.Entity<NarrationLog>()
                .HasIndex(x => new { x.RestaurantId, x.ListenTime });

            builder.Entity<NarrationLog>()
                .HasIndex(x => x.ListenTime);

            builder.Entity<OnlineWebPresence>()
                .HasKey(x => x.PresenceId);

            builder.Entity<OnlineWebPresence>()
                .HasIndex(x => x.LastSeenUtc);

            builder.Entity<OnlineWebPresence>()
                .HasIndex(x => new { x.RestaurantId, x.DeviceId, x.LastSeenUtc });

            builder.Entity<DeviceConnectionHistory>()
                .HasIndex(x => x.EventTimeUtc);

            builder.Entity<DeviceConnectionHistory>()
                .HasIndex(x => new { x.DeviceId, x.EventTimeUtc });
        }
    }
}