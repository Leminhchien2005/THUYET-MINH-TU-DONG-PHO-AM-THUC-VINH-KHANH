using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodStreetWeb.Controllers
{
    [Authorize(Roles = "RestaurantOwner")]
    public class OwnerController : Controller
    {
        public IActionResult OwnerDashboard()
        {
            return View();
        }
    }
}