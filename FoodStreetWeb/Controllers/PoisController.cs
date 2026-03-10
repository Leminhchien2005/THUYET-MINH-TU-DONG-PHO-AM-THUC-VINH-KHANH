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
        // DANH SÁCH POI
        // =========================
        public async Task<IActionResult> Index()
        {
            // Admin thấy tất cả
            if (User.IsInRole("Admin"))
            {
                return View(await _context.Pois.ToListAsync());
            }

            // Owner chỉ thấy quán của mình
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var myPois = await _context.Pois
                .Where(p => p.OwnerId == userId)
                .ToListAsync();

            return View(myPois);
        }

        // =========================
        // CREATE
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

                // Admin thêm → duyệt luôn
                if (User.IsInRole("Admin"))
                {
                    poi.Status = PoiStatus.Approved;
                }
                else
                {
                    // Owner thêm → chờ duyệt quán mới
                    poi.Status = PoiStatus.PendingCreate;
                }

                _context.Add(poi);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(poi);
        }

        // =========================
        // EDIT
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
                var oldPoi = await _context.Pois.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (oldPoi == null)
                    return NotFound();

                // Admin sửa → duyệt luôn
                if (User.IsInRole("Admin"))
                {
                    poi.Status = PoiStatus.Approved;
                }
                else
                {
                    // Owner sửa → chờ duyệt chỉnh sửa
                    poi.Status = PoiStatus.PendingUpdate;
                }

                poi.OwnerId = oldPoi.OwnerId;

                _context.Update(poi);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(poi);
        }

        // =========================
        // DELETE
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