using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using FoodStreetWeb.Data;
using FoodStreetWeb.Models;

namespace FoodStreetWeb.Controllers
{
    [Authorize(Roles = "Admin,RestaurantOwner")]
    public class PoisController : Controller
    {
        private readonly AppDbContext _context;

        public PoisController(AppDbContext context)
        {
            _context = context;
        }

        // =========================
        // Danh sách POI
        // =========================
        public async Task<IActionResult> Index()
        {
            if (User.IsInRole("Admin"))
            {
                return View(await _context.Pois.ToListAsync());
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var myPois = await _context.Pois
                .Where(p => p.OwnerId == userId)
                .ToListAsync();

            return View(myPois);
        }

        // =========================
        // Create
        // =========================
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Poi poi)
        {
            if (ModelState.IsValid)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                poi.OwnerId = userId;

                // luôn chờ duyệt
                poi.Status = PoiStatus.Pending;

                _context.Add(poi);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(poi);
        }

        // =========================
        // Edit
        // =========================
        public async Task<IActionResult> Edit(int id)
        {
            var poi = await _context.Pois.FindAsync(id);

            if (poi == null)
                return NotFound();

            return View(poi);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Poi poi)
        {
            if (id != poi.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                // khi sửa phải chờ duyệt lại
                poi.Status = PoiStatus.Pending;

                _context.Update(poi);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(poi);
        }

        // =========================
        // Delete
        // =========================
        public async Task<IActionResult> Delete(int id)
        {
            var poi = await _context.Pois.FindAsync(id);

            if (poi == null)
                return NotFound();

            return View(poi);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var poi = await _context.Pois.FindAsync(id);

            if (poi != null)
            {
                _context.Pois.Remove(poi);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================
        // ADMIN DUYỆT
        // =========================

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Approve(int id)
        {
            var poi = await _context.Pois.FindAsync(id);

            if (poi != null)
            {
                poi.Status = PoiStatus.Approved;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Reject(int id)
        {
            var poi = await _context.Pois.FindAsync(id);

            if (poi != null)
            {
                poi.Status = PoiStatus.Rejected;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}