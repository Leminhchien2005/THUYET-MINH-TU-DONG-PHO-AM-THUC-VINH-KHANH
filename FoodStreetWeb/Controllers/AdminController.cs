using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FoodStreetWeb.Models;
using FoodStreetWeb.Data;

namespace FoodStreetWeb.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppDbContext _context;

        public AdminController(UserManager<ApplicationUser> userManager,
                               AppDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // =============================
        // Dashboard
        // =============================
        public IActionResult AdminDashboard()
        {
            return View();
        }

        public IActionResult OnlineDeviceZones()
        {
            return View();
        }

        // =============================
        // Danh sách tài khoản + SEARCH
        // =============================
        public async Task<IActionResult> ManageUsers(string email, string role, string status)
        {
            var users = _userManager.Users.ToList();

            var userList = new List<UserViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);

                var userRole = roles.FirstOrDefault();

                var isLocked = user.LockoutEnd != null &&
                               user.LockoutEnd > DateTime.Now;

                userList.Add(new UserViewModel
                {
                    Id = user.Id,
                    Email = user.Email,
                    Role = userRole,
                    IsLocked = isLocked
                });
            }

            // =====================
            // SEARCH EMAIL
            // =====================
            if (!string.IsNullOrEmpty(email))
            {
                userList = userList
                    .Where(u => u.Email.Contains(email, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // =====================
            // SEARCH ROLE
            // =====================
            if (!string.IsNullOrEmpty(role))
            {
                userList = userList
                    .Where(u => u.Role == role)
                    .ToList();
            }

            // =====================
            // SEARCH STATUS
            // =====================
            if (!string.IsNullOrEmpty(status))
            {
                if (status == "Active")
                    userList = userList.Where(u => !u.IsLocked).ToList();

                if (status == "Locked")
                    userList = userList.Where(u => u.IsLocked).ToList();
            }

            return View(userList);
        }

        // =============================
        // Thêm tài khoản (GET)
        // =============================
        public IActionResult CreateUser()
        {
            return View();
        }

        // =============================
        // Thêm tài khoản (POST)
        // =============================
        [HttpPost]
        public async Task<IActionResult> CreateUser(CreateUserViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                PhoneNumber = model.PhoneNumber
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, model.Role);

                return RedirectToAction(nameof(ManageUsers));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        // =============================
        // Khóa tài khoản
        // =============================
        public async Task<IActionResult> LockUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user != null)
            {
                user.LockoutEnd = DateTimeOffset.MaxValue;
                await _userManager.UpdateAsync(user);
            }

            return RedirectToAction(nameof(ManageUsers));
        }

        // =============================
        // Mở khóa tài khoản
        // =============================
        public async Task<IActionResult> UnlockUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user != null)
            {
                user.LockoutEnd = null;
                await _userManager.UpdateAsync(user);
            }

            return RedirectToAction(nameof(ManageUsers));
        }

        // =============================
        // Chi tiết tài khoản
        // =============================
        public async Task<IActionResult> UserDetails(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
                return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            ViewBag.Role = roles.FirstOrDefault();

            var pois = await _context.Pois
                .Where(p => p.OwnerId == id)
                .ToListAsync();

            ViewBag.Pois = pois;

            return View(user);
        }
    }
}