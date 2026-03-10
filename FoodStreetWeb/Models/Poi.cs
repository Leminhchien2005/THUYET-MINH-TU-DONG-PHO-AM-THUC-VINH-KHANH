using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FoodStreetWeb.Models
{
    // trạng thái POI
    public enum PoiStatus
    {
        Pending,   // chờ admin duyệt
        Approved,  // đã duyệt
        Rejected   // bị từ chối
    }

    public class Poi
    {
        [Key]
        public int Id { get; set; }

        public string? Name { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        // bán kính kích hoạt (mét)
        public double Radius { get; set; }

        public string? Description { get; set; }

        public string? ImageUrl { get; set; }

        // =========================
        // CHỦ NHÀ HÀNG
        // =========================
        public string? OwnerId { get; set; }

        [ForeignKey("OwnerId")]
        public ApplicationUser? Owner { get; set; }

        // =========================
        // TRẠNG THÁI DUYỆT
        // =========================
        public PoiStatus Status { get; set; } = PoiStatus.Pending;

        // không lưu DB
        [NotMapped]
        public double DistanceKm { get; set; }
    }
}