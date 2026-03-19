using Microsoft.AspNetCore.Identity;

namespace FoodStreetWeb.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }
    }
}