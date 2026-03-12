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
        // YÊU CẦU ĐÃ GỬI
        // =========================
        public async Task<IActionResult> Requests()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var requests = await _context.PoiRequests
                .Where(r => r.OwnerId == userId)
                .ToListAsync();

            return View(requests);
        }

        // =========================
        // CHI TIẾT QUÁN
        // =========================
        public async Task<IActionResult> Details(int id)
        {
            var poi = await _context.Pois
                .FirstOrDefaultAsync(p => p.Id == id);

            if (poi == null)
                return NotFound();

            return View(poi);
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

                var request = new PoiRequest
                {
                    RequestType = PoiRequestType.Create,
                    OwnerId = userId,
                    Name = poi.Name,
                    Latitude = poi.Latitude,
                    Longitude = poi.Longitude,
                    Radius = poi.Radius,
                    Description = poi.Description,
                    ImageUrl = poi.ImageUrl,
                    Status = PoiStatus.PendingCreate
                };

                _context.PoiRequests.Add(request);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Requests));
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
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var request = new PoiRequest
                {
                    PoiId = id,
                    RequestType = PoiRequestType.Update,
                    OwnerId = userId,
                    Name = poi.Name,
                    Latitude = poi.Latitude,
                    Longitude = poi.Longitude,
                    Radius = poi.Radius,
                    Description = poi.Description,
                    ImageUrl = poi.ImageUrl,
                    Status = PoiStatus.PendingUpdate
                };

                _context.PoiRequests.Add(request);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Requests));
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
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var request = new PoiRequest
            {
                PoiId = id,
                OwnerId = userId,
                RequestType = PoiRequestType.Delete,
                Status = PoiStatus.PendingUpdate
            };

            _context.PoiRequests.Add(request);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Requests));
        }

        // =========================
        // ADMIN DUYỆT REQUEST
        // =========================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Approve(int id)
        {
            var request = await _context.PoiRequests.FindAsync(id);

            if (request == null)
                return NotFound();

            if (request.RequestType == PoiRequestType.Create)
            {
                var poi = new Poi
                {
                    OwnerId = request.OwnerId,
                    Name = request.Name,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    Radius = request.Radius,
                    Description = request.Description,
                    ImageUrl = request.ImageUrl,
                    Status = PoiStatus.Approved
                };

                _context.Pois.Add(poi);
            }

            if (request.RequestType == PoiRequestType.Update)
            {
                var poi = await _context.Pois.FindAsync(request.PoiId);

                if (poi != null)
                {
                    poi.Name = request.Name;
                    poi.Latitude = request.Latitude;
                    poi.Longitude = request.Longitude;
                    poi.Radius = request.Radius;
                    poi.Description = request.Description;
                    poi.ImageUrl = request.ImageUrl;
                }
            }

            if (request.RequestType == PoiRequestType.Delete)
            {
                var poi = await _context.Pois.FindAsync(request.PoiId);

                if (poi != null)
                {
                    _context.Pois.Remove(poi);
                }
            }

            request.Status = PoiStatus.Approved;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // =========================
        // ADMIN TỪ CHỐI
        // =========================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Reject(int id)
        {
            var request = await _context.PoiRequests.FindAsync(id);

            if (request != null)
            {
                request.Status = PoiStatus.Rejected;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}