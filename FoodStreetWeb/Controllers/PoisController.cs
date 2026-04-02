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
        private readonly TranslateService _translator;

        public PoisController(AppDbContext context, TranslateService translator)
        {
            _context = context;
            _translator = translator;
        }

        // =========================
        // DANH SÁCH POI
        // =========================
        public async Task<IActionResult> Index(string search)
        {
            var query = from poi in _context.Pois
                        join user in _context.Users
                        on poi.OwnerId equals user.Id
                        where poi.Status == PoiStatus.Approved
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
            // ======================
            // LẤY REQUEST
            // ======================
            var request = await _context.PoiRequests
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
                return NotFound();

            // ======================
            // LẤY POI CŨ + FOODS
            // ======================
            Poi oldPoi = null;

            if (request.PoiId != null)
            {
                oldPoi = await _context.Pois
                    .Include(p => p.Foods)
                    .FirstOrDefaultAsync(p => p.Id == request.PoiId);
            }

            ViewBag.OldPoi = oldPoi;

            // ======================
            // LẤY FOOD REQUESTS (món mới / chỉnh sửa)
            // ======================
            var foodRequests = await _context.FoodRequests
                .Where(f => f.PoiRequestId == id)
                .ToListAsync();

            ViewBag.FoodRequests = foodRequests;

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

            var foods = await _context.Foods
                .Where(f => f.PoiId == id)
                .ToListAsync();

            ViewBag.Foods = foods;

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
        public async Task<IActionResult> Create(CreatePoiWithFoodsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine(error.ErrorMessage);
                }
            }

            if (ModelState.IsValid)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // 1. Lưu PoiRequest
                var imageUrl = await SaveImage(model.ImageFile);

                var request = new PoiRequest
                {
                    RequestType = PoiRequestType.Create,
                    OwnerId = userId,
                    Name = model.Name,
                    Latitude = model.Latitude,
                    Longitude = model.Longitude,
                    Radius = model.Radius,
                    Description = model.Description,
                    ImageUrl = imageUrl,
                    Status = PoiStatus.PendingCreate,
                    CreatedAt = DateTime.Now
                };

                _context.PoiRequests.Add(request);
                await _context.SaveChangesAsync();

                // 2. Lưu FoodRequest
                if (model.Foods != null && model.Foods.Count > 0)
                {
                    foreach (var food in model.Foods)
                    {
                        var foodImage = await SaveImage(food.ImageFile);

                        var foodRequest = new FoodRequest
                        {
                            Name = food.Name,
                            Price = food.Price,
                            Description = food.Description,
                            ImageUrl = foodImage,
                            PoiRequestId = request.Id
                        };

                        _context.FoodRequests.Add(foodRequest);
                    }

                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Requests));
            }

            return View(model);
        }

        // =========================
        // EDIT
        // =========================
        public async Task<IActionResult> Edit(int id)
        {
            var poi = await _context.Pois.FindAsync(id);

            if (poi == null)
                return NotFound();

            var foods = await _context.Foods
                .Where(f => f.PoiId == id)
                .Select(f => new FoodEditItem
                {
                    Id = f.Id,
                    Name = f.Name,
                    Price = f.Price,
                    Description = f.Description,
                    ImageUrl = f.ImageUrl,
                    IsExisting = true
                })
                .ToListAsync();

            var vm = new PoiEditViewModel
            {
                Id = poi.Id,
                Name = poi.Name,
                Description = poi.Description,
                Latitude = poi.Latitude,
                Longitude = poi.Longitude,
                Radius = poi.Radius,
                ImageUrl = poi.ImageUrl,
                Foods = foods
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(PoiEditViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // =========================
            // 🔥 ADMIN → UPDATE THẲNG
            // =========================
            if (User.IsInRole("Admin"))
            {
                var poi = await _context.Pois
                    .Include(p => p.Foods)
                    .FirstOrDefaultAsync(p => p.Id == model.Id);

                if (poi == null)
                    return NotFound();

                // ===== update POI =====
                poi.Name = model.Name;
                poi.Latitude = model.Latitude;
                poi.Longitude = model.Longitude;
                poi.Radius = model.Radius;
                poi.Description = model.Description;
                if (model.ImageFile != null)
                {
                    poi.ImageUrl = await SaveImage(model.ImageFile);
                }

                // ===== update FOODS =====
                foreach (var food in model.Foods)
                {
                    if (food.Id > 0)
                    {
                        var existing = poi.Foods.FirstOrDefault(f => f.Id == food.Id);

                        if (existing != null)
                        {
                            existing.Name = food.Name;
                            existing.Price = food.Price;
                            existing.Description = food.Description;
                            if (food.ImageFile != null)
                            {
                                existing.ImageUrl = await SaveImage(food.ImageFile);
                            }
                        }
                    }
                    else
                    {
                        poi.Foods.Add(new Food
                        {
                            Name = food.Name,
                            Price = food.Price,
                            Description = food.Description,
                            ImageUrl = food.ImageUrl,
                            PoiId = poi.Id
                        });
                    }
                }

                await _context.SaveChangesAsync(); // ✅ chỉ 1 lần ở đây

                // =========================
                // 🔥 UPDATE TRANSLATION
                // =========================
                var languages = new[] { "vi", "en", "zh" };

                // ===== POI =====
                foreach (var lang in languages)
                {
                    var trans = await _context.PoiTranslations
                        .FirstOrDefaultAsync(x => x.PoiId == poi.Id && x.LanguageCode == lang);

                    if (trans == null)
                    {
                        trans = new PoiTranslation
                        {
                            PoiId = poi.Id,
                            LanguageCode = lang
                        };

                        _context.PoiTranslations.Add(trans);
                    }

                    trans.Name = poi.Name; // ❌ không dịch tên

                    if (lang == "vi")
                    {
                        trans.Description = poi.Description;
                    }
                    else
                    {
                        trans.Description = await _translator.Translate(poi.Description ?? "", "vi", lang);
                    }
                }

                // ===== FOOD =====
                var foods = await _context.Foods
                    .Where(x => x.PoiId == poi.Id)
                    .ToListAsync();

                foreach (var food in foods)
                {
                    foreach (var lang in languages)
                    {
                        var trans = await _context.FoodTranslations
                            .FirstOrDefaultAsync(x => x.FoodId == food.Id && x.LanguageCode == lang);

                        if (trans == null)
                        {
                            trans = new FoodTranslation
                            {
                                FoodId = food.Id,
                                LanguageCode = lang
                            };

                            _context.FoodTranslations.Add(trans);
                        }

                        if (lang == "vi")
                        {
                            trans.Name = food.Name;
                            trans.Description = food.Description;
                        }
                        else
                        {
                            trans.Name = await _translator.Translate(food.Name, "vi", lang);
                            trans.Description = await _translator.Translate(food.Description ?? "", "vi", lang);
                        }
                    }
                }

                await _context.SaveChangesAsync(); // lưu translation

                return RedirectToAction("Index");
            }

            // =========================
            // 👤 USER → REQUEST
            // =========================
            var imageUrl = model.ImageUrl;

            if (model.ImageFile != null)
            {
                imageUrl = await SaveImage(model.ImageFile);
            }

            var poiRequest = new PoiRequest
            {
                PoiId = model.Id,
                OwnerId = userId,
                RequestType = PoiRequestType.Update,
                Name = model.Name,
                Latitude = model.Latitude,
                Longitude = model.Longitude,
                Radius = model.Radius,
                Description = model.Description,
                ImageUrl = imageUrl, // ✅ dùng ảnh mới nếu có
                Status = PoiStatus.PendingUpdate,
                CreatedAt = DateTime.Now
            };

            _context.PoiRequests.Add(poiRequest);
            await _context.SaveChangesAsync();

            if (model.Foods != null)
            {
                foreach (var food in model.Foods)
                {
                    var foodImage = food.ImageUrl;

                    if (food.ImageFile != null)
                    {
                        foodImage = await SaveImage(food.ImageFile);
                    }

                    _context.FoodRequests.Add(new FoodRequest
                    {
                        PoiRequestId = poiRequest.Id,
                        FoodId = food.Id,
                        Name = food.Name,
                        Price = food.Price,
                        Description = food.Description,
                        ImageUrl = foodImage // ✅ FIX ở đây
                    });
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Requests");
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
                    poi.Status = PoiStatus.Rejected;

                    _context.Pois.Update(poi);
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
                await _context.SaveChangesAsync(); // có poi.Id

                // ===== COPY FOOD =====
                var foodRequests = await _context.FoodRequests
                    .Where(x => x.PoiRequestId == request.Id)
                    .ToListAsync();

                var newFoods = new List<Food>();

                foreach (var fr in foodRequests)
                {
                    var food = new Food
                    {
                        Name = fr.Name,
                        Price = fr.Price,
                        Description = fr.Description,
                        ImageUrl = fr.ImageUrl,
                        PoiId = poi.Id
                    };

                    newFoods.Add(food);
                }

                _context.Foods.AddRange(newFoods);
                await _context.SaveChangesAsync(); // có food.Id

                // =========================
                // 🔥 DỊCH
                // =========================
                // ===== VI (gốc) =====
                _context.PoiTranslations.Add(new PoiTranslation
                {
                    PoiId = poi.Id,
                    LanguageCode = "vi",
                    Name = poi.Name,
                    Description = poi.Description
                });

                // ===== EN + ZH =====
                var languages = new[] { "en", "zh" };

                foreach (var lang in languages)
                {
                    _context.PoiTranslations.Add(new PoiTranslation
                    {
                        PoiId = poi.Id,
                        LanguageCode = lang,
                        Name = poi.Name,
                        Description = await _translator.Translate(poi.Description ?? "", "vi", lang)
                    });
                }

                // ===== FOOD TRANSLATION =====
                foreach (var food in newFoods)
                {
                    // ===== VI =====
                    _context.FoodTranslations.Add(new FoodTranslation
                    {
                        FoodId = food.Id,
                        LanguageCode = "vi",
                        Name = food.Name,
                        Description = food.Description
                    });

                    // ===== EN + ZH =====
                    foreach (var lang in languages)
                    {
                        _context.FoodTranslations.Add(new FoodTranslation
                        {
                            FoodId = food.Id,
                            LanguageCode = lang,
                            Name = await _translator.Translate(food.Name, "vi", lang),
                            Description = await _translator.Translate(food.Description ?? "", "vi", lang)
                        });
                    }
                }

                await _context.SaveChangesAsync(); // lưu translation
            }
            else if (request.RequestType == PoiRequestType.Update)
            {
                var poi = await _context.Pois.FindAsync(request.PoiId);

                if (poi != null)
                {
                    // ===== UPDATE POI =====
                    poi.Name = request.Name;
                    poi.Latitude = request.Latitude;
                    poi.Longitude = request.Longitude;
                    poi.Radius = request.Radius;
                    poi.Description = request.Description;
                    poi.ImageUrl = request.ImageUrl;
                }

                // ===== FOOD SYNC =====
                var foodRequests = await _context.FoodRequests
                    .Where(f => f.PoiRequestId == request.Id)
                    .ToListAsync();

                var oldFoods = await _context.Foods
                    .Where(f => f.PoiId == request.PoiId)
                    .ToListAsync();

                foreach (var fr in foodRequests)
                {
                    if (fr.FoodId.HasValue)
                    {
                        var existing = oldFoods.FirstOrDefault(x => x.Id == fr.FoodId.Value);

                        if (existing != null)
                        {
                            existing.Name = fr.Name;
                            existing.Price = fr.Price;
                            existing.Description = fr.Description;
                            existing.ImageUrl = fr.ImageUrl;
                        }
                    }
                    else
                    {
                        _context.Foods.Add(new Food
                        {
                            Name = fr.Name,
                            Price = fr.Price,
                            Description = fr.Description,
                            ImageUrl = fr.ImageUrl,
                            PoiId = request.PoiId.Value
                        });
                    }
                }

                await _context.SaveChangesAsync(); // 🔥 phải save trước

                // =========================
                // 🔥 UPDATE TRANSLATION
                // =========================
                var languages = new[] { "vi", "en", "zh" };

                // ===== POI =====
                foreach (var lang in languages)
                {
                    var trans = await _context.PoiTranslations
                        .FirstOrDefaultAsync(x => x.PoiId == poi.Id && x.LanguageCode == lang);

                    if (trans == null)
                    {
                        trans = new PoiTranslation
                        {
                            PoiId = poi.Id,
                            LanguageCode = lang
                        };

                        _context.PoiTranslations.Add(trans);
                    }

                    trans.Name = poi.Name; // ❌ không dịch tên

                    if (lang == "vi")
                    {
                        trans.Description = poi.Description;
                    }
                    else
                    {
                        trans.Description = await _translator.Translate(poi.Description ?? "", "vi", lang);
                    }
                }

                // ===== FOOD =====
                var foods = await _context.Foods
                    .Where(x => x.PoiId == poi.Id)
                    .ToListAsync();

                foreach (var food in foods)
                {
                    foreach (var lang in languages)
                    {
                        var trans = await _context.FoodTranslations
                            .FirstOrDefaultAsync(x => x.FoodId == food.Id && x.LanguageCode == lang);

                        if (trans == null)
                        {
                            trans = new FoodTranslation
                            {
                                FoodId = food.Id,
                                LanguageCode = lang
                            };

                            _context.FoodTranslations.Add(trans);
                        }

                        if (lang == "vi")
                        {
                            trans.Name = food.Name;
                            trans.Description = food.Description;
                        }
                        else
                        {
                            trans.Name = await _translator.Translate(food.Name, "vi", lang);
                            trans.Description = await _translator.Translate(food.Description ?? "", "vi", lang);
                        }
                    }
                }

                await _context.SaveChangesAsync();
            }
            else if (request.RequestType == PoiRequestType.Delete)
            {
                var poi = await _context.Pois.FindAsync(request.PoiId);

                if (poi != null)
                {
                    poi.Status = PoiStatus.Rejected;

                    _context.Pois.Update(poi);
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

        private async Task<string> SaveImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return null;

            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);

            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images", fileName);

            using var stream = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(stream);

            return "/images/" + fileName;
        }

    }
}