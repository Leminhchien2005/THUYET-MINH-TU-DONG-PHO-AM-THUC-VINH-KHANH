using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using FoodStreetWeb.Models;

namespace FoodStreetWeb.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // =========================
        // Trang đăng ký
        // =========================
        public IActionResult Register()
        {
            return View();
        }

        // =========================
        // Xử lý đăng ký
        // =========================
        [HttpPost]
        public async Task<IActionResult> Register(
            string FullName,
            string Email,
            string PhoneNumber,
            string Password,
            string ConfirmPassword)
        {
            // kiểm tra mật khẩu
            if (Password != ConfirmPassword)
            {
                ViewBag.Error = "Mật khẩu không khớp";
                return View();
            }

            var user = new ApplicationUser
            {
                UserName = Email,
                Email = Email,
                PhoneNumber = PhoneNumber,
                FullName = FullName
            };

            var result = await _userManager.CreateAsync(user, Password);

            if (result.Succeeded)
            {
                // mặc định role chủ nhà hàng
                await _userManager.AddToRoleAsync(user, "RestaurantOwner");

                TempData["Success"] = "Đăng ký thành công";

                return RedirectToAction("Login");
            }

            ViewBag.Error = "Đăng ký thất bại";
            return View();
        }

        // =========================
        // Trang Login
        // =========================
        public IActionResult Login()
        {
            return View();
        }

        // =========================
        // Xử lý Login
        // =========================
        [HttpPost]
        public async Task<IActionResult> Login(string Email, string Password)
        {
            var result = await _signInManager.PasswordSignInAsync(
                Email,
                Password,
                false,
                false);

            if (result.Succeeded)
            {
                var user = await _userManager.FindByEmailAsync(Email);

                if (user != null)
                {
                    // Admin
                    if (await _userManager.IsInRoleAsync(user, "Admin"))
                    {
                        return RedirectToAction("AdminDashboard", "Admin");
                    }

                    // Chủ nhà hàng
                    if (await _userManager.IsInRoleAsync(user, "RestaurantOwner"))
                    {
                        return RedirectToAction("OwnerDashboard", "Owner");
                    }
                }

                return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = "Sai tài khoản hoặc mật khẩu";
            return View();
        }

        // =========================
        // Logout
        // =========================
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();

            return RedirectToAction("Login");
        }
    }
}