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
        public async Task<IActionResult> Index(string search)
        {
            var query = from poi in _context.Pois
                        join user in _context.Users
                        on poi.OwnerId equals user.Id
                        select new PoiListViewModel
                        {
                            Id = poi.Id,
                            Name = poi.Name,
                            Latitude = poi.Latitude,
                            Longitude = poi.Longitude,
                            Radius = poi.Radius,
                            Description = poi.Description,
                            ImageUrl = poi.ImageUrl,
                            OwnerName = user.FullName
                        };

            // ADMIN tìm theo tên quán hoặc chủ
            if (User.IsInRole("Admin"))
            {
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(p =>
                        p.Name.Contains(search) ||
                        p.OwnerName.Contains(search));
                }
            }
            else
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                query = query.Where(p =>
                    _context.Pois.Any(x => x.Id == p.Id && x.OwnerId == userId));

                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(p => p.Name.Contains(search));
                }
            }

            var pois = await query.ToListAsync();

            return View(pois);
        }

        // =========================
        // OWNER - REQUEST ĐÃ GỬI
        // =========================
        public async Task<IActionResult> Requests(
            string search,
            string type,
            string status,
            DateTime? date)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var query = _context.PoiRequests
                .Include(r => r.Poi)
                .Where(r => r.OwnerId == userId)
                .AsQueryable();

            // tìm theo tên quán
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(r => r.Name.Contains(search));
            }

            // lọc loại yêu cầu
            if (!string.IsNullOrEmpty(type))
            {
                if (Enum.TryParse<PoiRequestType>(type, out var t))
                {
                    query = query.Where(r => r.RequestType == t);
                }
            }

            // lọc trạng thái
            if (!string.IsNullOrEmpty(status))
            {
                if (Enum.TryParse<PoiStatus>(status, out var s))
                {
                    query = query.Where(r => r.Status == s);
                }
            }

            // lọc theo ngày gửi
            if (date.HasValue)
            {
                query = query.Where(r => r.CreatedAt.Date == date.Value.Date);
            }

            var requests = await query
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(requests);
        }

        // =========================
        // ADMIN - DANH SÁCH REQUEST
        // =========================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminRequests(
            string search,
            string status,
            string type,
            DateTime? date)
        {
            var query = _context.PoiRequests
                .Include(r => r.Poi)
                .AsQueryable();

            // tìm theo tên quán
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(r => r.Name.Contains(search));
            }

            // lọc trạng thái
            if (!string.IsNullOrEmpty(status))
            {
                if (Enum.TryParse<PoiStatus>(status, out var s))
                {
                    query = query.Where(r => r.Status == s);
                }
            }

            // lọc loại yêu cầu
            if (!string.IsNullOrEmpty(type))
            {
                if (Enum.TryParse<PoiRequestType>(type, out var t))
                {
                    query = query.Where(r => r.RequestType == t);
                }
            }

            // lọc theo ngày gửi
            if (date.HasValue)
            {
                query = query.Where(r => r.CreatedAt.Date == date.Value.Date);
            }

            var requests = await query
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var pendingCount = await _context.PoiRequests
                .CountAsync(r =>
                    r.Status == PoiStatus.PendingCreate ||
                    r.Status == PoiStatus.PendingUpdate);

            ViewBag.PendingCount = pendingCount;

            return View(requests);
        }

        // =========================
        // CHI TIẾT REQUEST
        // =========================
        public async Task<IActionResult> RequestDetails(int id)
        {
            var request = await _context.PoiRequests
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
                return NotFound();

            // lấy quán cũ (nếu có)
            Poi oldPoi = null;

            if (request.PoiId != null)
            {
                oldPoi = await _context.Pois
                    .FirstOrDefaultAsync(p => p.Id == request.PoiId);
            }

            ViewBag.OldPoi = oldPoi;

            // ======================
            // LẤY CHỦ NHÀ HÀNG
            // ======================
            var owner = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == request.OwnerId);

            ViewBag.OwnerName = owner?.FullName;
            ViewBag.OwnerEmail = owner?.Email;
            ViewBag.OwnerPhone = owner?.PhoneNumber;

            return View(request);
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

            // Lấy thông tin chủ quán
            var owner = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == poi.OwnerId);

            ViewBag.OwnerName = owner?.FullName;
            ViewBag.OwnerEmail = owner?.Email;
            ViewBag.OwnerPhone = owner?.PhoneNumber;

            return View(poi);
        }

        // =========================
        // CREATE
        // =========================
        [Authorize(Roles = "RestaurantOwner")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "RestaurantOwner")]
        public async Task<IActionResult> Create(Poi poi)
        {
            if (ModelState.IsValid)
            {
                // ADMIN tạo trực tiếp
                if (User.IsInRole("Admin"))
                {
                    poi.Status = PoiStatus.Approved;
                    _context.Pois.Add(poi);
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Index));
                }

                // OWNER tạo request
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
                    Status = PoiStatus.PendingCreate,
                    CreatedAt = DateTime.Now
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

            // tránh sửa quán người khác
            if (!User.IsInRole("Admin"))
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (poi.OwnerId != userId)
                    return Unauthorized();
            }

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
                // ADMIN sửa trực tiếp
                if (User.IsInRole("Admin"))
                {
                    var existingPoi = await _context.Pois.FindAsync(id);

                    if (existingPoi == null)
                        return NotFound();

                    existingPoi.Name = poi.Name;
                    existingPoi.Latitude = poi.Latitude;
                    existingPoi.Longitude = poi.Longitude;
                    existingPoi.Radius = poi.Radius;
                    existingPoi.Description = poi.Description;
                    existingPoi.ImageUrl = poi.ImageUrl;

                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Index));
                }

                // OWNER gửi request
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
                    Status = PoiStatus.PendingUpdate,
                    CreatedAt = DateTime.Now
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
            // ADMIN xóa trực tiếp
            if (User.IsInRole("Admin"))
            {
                var poi = await _context.Pois.FindAsync(id);

                if (poi != null)
                {
                    _context.Pois.Remove(poi);
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }

            // OWNER gửi request
            var poiOld = await _context.Pois.FindAsync(id);

            if (poiOld == null)
                return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var request = new PoiRequest
            {
                PoiId = id,
                OwnerId = userId,
                RequestType = PoiRequestType.Delete,

                Name = poiOld.Name,
                Latitude = poiOld.Latitude,
                Longitude = poiOld.Longitude,
                Radius = poiOld.Radius,
                Description = poiOld.Description,
                ImageUrl = poiOld.ImageUrl,

                Status = PoiStatus.PendingUpdate,
                CreatedAt = DateTime.Now
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
            else if (request.RequestType == PoiRequestType.Update)
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
            else if (request.RequestType == PoiRequestType.Delete)
            {
                var poi = await _context.Pois.FindAsync(request.PoiId);

                if (poi != null)
                {
                    _context.Pois.Remove(poi);
                }
            }

            request.Status = PoiStatus.Approved;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(AdminRequests));
        }

        // =========================
        // ADMIN TỪ CHỐI
        // =========================
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            var request = await _context.PoiRequests.FindAsync(id);

            if (request != null)
            {
                request.Status = PoiStatus.Rejected;
                request.RejectReason = reason;   // lưu lý do

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(AdminRequests));
        }
    }
}