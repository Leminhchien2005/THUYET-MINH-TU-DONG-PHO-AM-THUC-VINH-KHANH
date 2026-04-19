using FoodStreetWeb.Data;
using FoodStreetWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodStreetWeb.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScanAnalyticsController : ControllerBase
    {
        private static readonly string[] DayNamesMonToSun =
        {
            "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"
        };

        private readonly AppDbContext _context;
        private readonly OnlineUsersService _onlineUsersService;

        public ScanAnalyticsController(AppDbContext context, OnlineUsersService onlineUsersService)
        {
            _context = context;
            _onlineUsersService = onlineUsersService;
        }

        // 1) Tổng lượt quét + lượt quét theo quán + top quán
        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview(
            [FromQuery] int? restaurantId = null,
            [FromQuery] DateTime? fromUtc = null,
            [FromQuery] DateTime? toUtc = null,
            [FromQuery] int top = 5)
        {
            var query = BuildFilteredQuery(restaurantId, fromUtc, toUtc);

            var totalScans = await query.CountAsync();

            var byRestaurantRaw = await query
                .GroupBy(x => x.RestaurantId)
                .Select(g => new
                {
                    RestaurantId = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            var restaurantIds = byRestaurantRaw.Select(x => x.RestaurantId).ToList();

            var names = await _context.Pois
                .AsNoTracking()
                .Where(x => restaurantIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Name })
                .ToDictionaryAsync(x => x.Id, x => x.Name ?? $"Restaurant #{x.Id}");

            var byRestaurant = byRestaurantRaw.Select(x => new RestaurantScanCountDto
            {
                RestaurantId = x.RestaurantId,
                RestaurantName = names.TryGetValue(x.RestaurantId, out var name) ? name : $"Restaurant #{x.RestaurantId}",
                Count = x.Count
            }).ToList();

            var topRestaurants = byRestaurant.Take(Math.Max(1, top)).ToList();

            return Ok(new OverviewResponse
            {
                TotalScans = totalScans,
                RestaurantId = restaurantId,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                ByRestaurant = byRestaurant,
                TopRestaurants = topRestaurants
            });
        }

        // 2) Theo giờ / thứ trong tuần / timeline
        [HttpGet("patterns")]
        public async Task<IActionResult> GetPatterns(
            [FromQuery] int? restaurantId = null,
            [FromQuery] DateTime? fromUtc = null,
            [FromQuery] DateTime? toUtc = null)
        {
            var query = BuildFilteredQuery(restaurantId, fromUtc, toUtc);

            var hourlyRaw = await query
                .GroupBy(x => x.ScanTime.Hour)
                .Select(g => new { Hour = g.Key, Count = g.Count() })
                .ToListAsync();

            var hourly = Enumerable.Range(0, 24)
                .Select(hour => new HourlyPointDto
                {
                    Hour = hour,
                    Count = hourlyRaw.FirstOrDefault(x => x.Hour == hour)?.Count ?? 0
                })
                .ToList();

            var weekdayRaw = await query
                .GroupBy(x => (int)x.ScanTime.DayOfWeek)
                .Select(g => new { DayOfWeek = g.Key, Count = g.Count() })
                .ToListAsync();

            var weekday = Enumerable.Range(0, 7)
                .Select(index =>
                {
                    var dayValue = ToDotNetDayOfWeek(index);
                    var found = weekdayRaw.FirstOrDefault(x => x.DayOfWeek == dayValue);
                    return new WeekdayPointDto
                    {
                        DayOfWeek = DayNamesMonToSun[index],
                        Count = found?.Count ?? 0
                    };
                })
                .ToList();

            var timeline = await query
                .GroupBy(x => x.ScanTime.Date)
                .Select(g => new TimelinePointDto
                {
                    Date = g.Key,
                    Count = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            return Ok(new PatternResponse
            {
                RestaurantId = restaurantId,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                Hourly = hourly,
                Weekday = weekday,
                Timeline = timeline
            });
        }

        // 3) Heatmap hour + dayOfWeek + count (+ crowd level)
        [HttpGet("heatmap")]
        public async Task<IActionResult> GetHeatmap(
            [FromQuery] int? restaurantId = null,
            [FromQuery] DateTime? fromUtc = null,
            [FromQuery] DateTime? toUtc = null,
            [FromQuery] int? threshold = null)
        {
            var query = BuildFilteredQuery(restaurantId, fromUtc, toUtc);

            // 4) LINQ GroupBy Hour + DayOfWeek
            var heatmapRaw = await query
                .GroupBy(x => new { Hour = x.ScanTime.Hour, DayOfWeek = (int)x.ScanTime.DayOfWeek })
                .Select(g => new
                {
                    g.Key.Hour,
                    g.Key.DayOfWeek,
                    Count = g.Count()
                })
                .ToListAsync();

            var avg = heatmapRaw.Count == 0 ? 0d : heatmapRaw.Average(x => x.Count);
            var highThreshold = threshold ?? (int)Math.Ceiling(avg * 1.2);
            var lowThreshold = (int)Math.Floor(avg * 0.8);

            var heatmap = heatmapRaw
                .Select(x => new
                {
                    DayOrder = ToMonToSunIndex(x.DayOfWeek),
                    Point = new HeatmapPointDto
                    {
                        Hour = x.Hour,
                        DayOfWeek = DayNamesMonToSun[ToMonToSunIndex(x.DayOfWeek)],
                        Count = x.Count,
                        CrowdLevel = GetCrowdLevel(x.Count, highThreshold, lowThreshold)
                    }
                })
                .OrderBy(x => x.DayOrder)
                .ThenBy(x => x.Point.Hour)
                .Select(x => x.Point)
                .ToList();

            return Ok(new HeatmapResponse
            {
                RestaurantId = restaurantId,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                Threshold = highThreshold,
                Points = heatmap
            });
        }

        [HttpGet("crowded-restaurants")]
        public async Task<IActionResult> GetCrowdedRestaurants([FromQuery] int days = 7)
        {
            days = Math.Clamp(days, 1, 30);

            var fromScanTime = NormalizeFilterToScanTime(DateTime.UtcNow.AddDays(-days)) ?? DateTime.UtcNow.AddDays(-days);

            var raw = await _context.ScanLogs
                .AsNoTracking()
                .Where(x => x.ScanTime >= fromScanTime)
                .GroupBy(x => new { x.RestaurantId, Hour = x.ScanTime.Hour, DayOfWeek = (int)x.ScanTime.DayOfWeek })
                .Select(g => new
                {
                    g.Key.RestaurantId,
                    Count = g.Count()
                })
                .ToListAsync();

            if (raw.Count == 0)
            {
                return Ok(new List<int>());
            }

            var crowdedRestaurantIds = raw
                .GroupBy(x => x.RestaurantId)
                .Select(group =>
                {
                    var avg = group.Average(x => x.Count);
                    var highThreshold = (int)Math.Ceiling(avg * 1.2);
                    var maxCount = group.Max(x => x.Count);
                    var totalCount = group.Sum(x => x.Count);
                    var isCrowded = group.Any(x => x.Count > highThreshold);

                    return new
                    {
                        RestaurantId = group.Key,
                        IsCrowded = isCrowded,
                        MaxCount = maxCount,
                        TotalCount = totalCount
                    };
                })
                .Where(x => x.IsCrowded)
                .OrderByDescending(x => x.MaxCount)
                .ThenByDescending(x => x.TotalCount)
                .Select(x => x.RestaurantId)
                .ToList();

            return Ok(crowdedRestaurantIds);
        }

        // 7) Bonus: so sánh giữa 2 ngày
        [HttpGet("compare-days")]
        public async Task<IActionResult> CompareDays(
            [FromQuery] DateTime dayA,
            [FromQuery] DateTime dayB,
            [FromQuery] int? restaurantId = null)
        {
            var normalizedDayA = NormalizeFilterToScanTime(dayA)?.Date ?? dayA.Date;
            var normalizedDayB = NormalizeFilterToScanTime(dayB)?.Date ?? dayB.Date;

            var start = normalizedDayA < normalizedDayB ? normalizedDayA : normalizedDayB;
            var end = (normalizedDayA > normalizedDayB ? normalizedDayA : normalizedDayB).AddDays(1);

            var query = _context.ScanLogs
                .AsNoTracking()
                .Where(x => x.ScanTime >= start && x.ScanTime < end);

            if (restaurantId.HasValue)
            {
                query = query.Where(x => x.RestaurantId == restaurantId.Value);
            }

            var raw = await query
                .GroupBy(x => new { Date = x.ScanTime.Date, Hour = x.ScanTime.Hour })
                .Select(g => new
                {
                    g.Key.Date,
                    g.Key.Hour,
                    Count = g.Count()
                })
                .ToListAsync();

            var pointsA = Enumerable.Range(0, 24)
                .Select(hour => new HourlyPointDto
                {
                    Hour = hour,
                    Count = raw.FirstOrDefault(x => x.Date == normalizedDayA && x.Hour == hour)?.Count ?? 0
                })
                .ToList();

            var pointsB = Enumerable.Range(0, 24)
                .Select(hour => new HourlyPointDto
                {
                    Hour = hour,
                    Count = raw.FirstOrDefault(x => x.Date == normalizedDayB && x.Hour == hour)?.Count ?? 0
                })
                .ToList();

            return Ok(new CompareDaysResponse
            {
                RestaurantId = restaurantId,
                DayA = normalizedDayA,
                DayB = normalizedDayB,
                DayAHourly = pointsA,
                DayBHourly = pointsB
            });
        }

        // 8) Tổng hợp lưu lượng web + app trong cùng 1 API (xử lý server-side)
        [HttpGet("traffic-overview")]
        public async Task<IActionResult> GetTrafficOverview(
            [FromQuery] int? restaurantId = null,
            [FromQuery] DateTime? fromUtc = null,
            [FromQuery] DateTime? toUtc = null)
        {
            var from = fromUtc ?? DateTime.UtcNow.AddDays(-6);
            var to = toUtc ?? DateTime.UtcNow;

            if (to < from)
            {
                (from, to) = (to, from);
            }

            var appQuery = _context.DeviceConnectionHistories
                .AsNoTracking()
                .Where(x => x.EventTimeUtc >= from && x.EventTimeUtc <= to)
                .Where(x => !x.ConnectionId.StartsWith("web:"));

            var webHistoryQuery = _context.DeviceConnectionHistories
                .AsNoTracking()
                .Where(x => x.EventTimeUtc >= from && x.EventTimeUtc <= to)
                .Where(x => x.ConnectionId.StartsWith("web:"));

            var webQuery = _context.OnlineWebPresences
                .AsNoTracking()
                .Where(x => x.LastSeenUtc >= from && x.LastSeenUtc <= to);

            if (restaurantId.HasValue)
            {
                webQuery = webQuery.Where(x => x.RestaurantId == restaurantId.Value);
                var suffix = $":{restaurantId.Value}";
                webHistoryQuery = webHistoryQuery.Where(x => EF.Functions.Like(x.Note ?? string.Empty, $"%{suffix}"));
            }

            var appConnectCount = await appQuery.CountAsync(x => x.EventType == "connect");
            var appDisconnectCount = await appQuery.CountAsync(x => x.EventType == "disconnect");
            var appUniqueDevices = await appQuery.Select(x => x.DeviceId).Distinct().CountAsync();

            var webConnectCount = await webHistoryQuery.CountAsync(x => x.EventType == "connect");
            var webDisconnectCount = await webHistoryQuery.CountAsync(x => x.EventType == "disconnect");

            int webActiveCount;
            try
            {
                webActiveCount = await _onlineUsersService.GetRestaurantDetailOnlineCountAsync(restaurantId);
            }
            catch
            {
                var webActiveCutoff = DateTime.UtcNow.AddSeconds(-15);
                var webActiveQuery = _context.OnlineWebPresences
                    .AsNoTracking()
                    .Where(x => x.LastSeenUtc >= webActiveCutoff);

                if (restaurantId.HasValue)
                {
                    webActiveQuery = webActiveQuery.Where(x => x.RestaurantId == restaurantId.Value);
                }

                webActiveCount = await webActiveQuery
                    .Select(x => x.DeviceId)
                    .Distinct()
                    .CountAsync();
            }
            var webUniqueDevices = await webHistoryQuery.Select(x => x.DeviceId).Distinct().CountAsync();

            var webByRole = await webQuery
                .GroupBy(x => x.Role)
                .Select(g => new { role = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToListAsync();

            var appDailyRaw = await appQuery
                .Where(x => x.EventType == "connect")
                .GroupBy(x => x.EventTimeUtc.Date)
                .Select(g => new { date = g.Key, count = g.Count() })
                .ToListAsync();

            var webDailyRaw = await webHistoryQuery
                .Where(x => x.EventType == "connect")
                .GroupBy(x => x.EventTimeUtc.Date)
                .Select(g => new { date = g.Key, count = g.Count() })
                .ToListAsync();

            var timeline = Enumerable.Range(0, (to.Date - from.Date).Days + 1)
                .Select(offset => from.Date.AddDays(offset))
                .Select(date => new
                {
                    date,
                    appCount = appDailyRaw.FirstOrDefault(x => x.date == date)?.count ?? 0,
                    webCount = webDailyRaw.FirstOrDefault(x => x.date == date)?.count ?? 0
                })
                .ToList();

            return Ok(new
            {
                fromUtc = from,
                toUtc = to,
                restaurantId,
                app = new
                {
                    connectCount = appConnectCount,
                    disconnectCount = appDisconnectCount,
                    uniqueDevices = appUniqueDevices
                },
                web = new
                {
                    connectCount = webConnectCount,
                    disconnectCount = webDisconnectCount,
                    activeCount = webActiveCount,
                    uniqueDevices = webUniqueDevices,
                    byRole = webByRole
                },
                timeline
            });
        }

        private IQueryable<FoodStreetWeb.Models.ScanLog> BuildFilteredQuery(int? restaurantId, DateTime? fromUtc, DateTime? toUtc)
        {
            var query = _context.ScanLogs.AsNoTracking().AsQueryable();

            var from = NormalizeFilterToScanTime(fromUtc);
            var to = NormalizeFilterToScanTime(toUtc);

            if (restaurantId.HasValue)
                query = query.Where(x => x.RestaurantId == restaurantId.Value);

            if (from.HasValue)
                query = query.Where(x => x.ScanTime >= from.Value);

            if (to.HasValue)
                query = query.Where(x => x.ScanTime <= to.Value);

            return query;
        }

        private static DateTime? NormalizeFilterToScanTime(DateTime? value)
        {
            if (!value.HasValue)
                return null;

            return value.Value.Kind switch
            {
                DateTimeKind.Utc => value.Value.AddHours(7),
                DateTimeKind.Unspecified => value.Value,
                _ => value.Value
            };
        }

        private static int ToDotNetDayOfWeek(int monToSunIndex)
        {
            // Mon..Sun => 1..6,0
            return monToSunIndex switch
            {
                0 => 1,
                1 => 2,
                2 => 3,
                3 => 4,
                4 => 5,
                5 => 6,
                _ => 0
            };
        }

        private static int ToMonToSunIndex(int dotNetDayOfWeek)
        {
            // .NET DayOfWeek: Sun=0, Mon=1 ... Sat=6
            return dotNetDayOfWeek switch
            {
                1 => 0,
                2 => 1,
                3 => 2,
                4 => 3,
                5 => 4,
                6 => 5,
                _ => 6
            };
        }

        private static string GetCrowdLevel(int count, int highThreshold, int lowThreshold)
        {
            if (count > highThreshold) return "Đông";
            if (count < lowThreshold) return "Vắng";
            return "Bình thường";
        }

        public class RestaurantScanCountDto
        {
            public int RestaurantId { get; set; }
            public string RestaurantName { get; set; } = string.Empty;
            public int Count { get; set; }
        }

        public class HourlyPointDto
        {
            public int Hour { get; set; }
            public int Count { get; set; }
        }

        public class WeekdayPointDto
        {
            public string DayOfWeek { get; set; } = string.Empty;
            public int Count { get; set; }
        }

        public class TimelinePointDto
        {
            public DateTime Date { get; set; }
            public int Count { get; set; }
        }

        public class HeatmapPointDto
        {
            public int Hour { get; set; }
            public string DayOfWeek { get; set; } = string.Empty;
            public int Count { get; set; }
            public string CrowdLevel { get; set; } = string.Empty;
        }

        public class OverviewResponse
        {
            public int TotalScans { get; set; }
            public int? RestaurantId { get; set; }
            public DateTime? FromUtc { get; set; }
            public DateTime? ToUtc { get; set; }
            public List<RestaurantScanCountDto> ByRestaurant { get; set; } = new();
            public List<RestaurantScanCountDto> TopRestaurants { get; set; } = new();
        }

        public class PatternResponse
        {
            public int? RestaurantId { get; set; }
            public DateTime? FromUtc { get; set; }
            public DateTime? ToUtc { get; set; }
            public List<HourlyPointDto> Hourly { get; set; } = new();
            public List<WeekdayPointDto> Weekday { get; set; } = new();
            public List<TimelinePointDto> Timeline { get; set; } = new();
        }

        public class HeatmapResponse
        {
            public int? RestaurantId { get; set; }
            public DateTime? FromUtc { get; set; }
            public DateTime? ToUtc { get; set; }
            public int Threshold { get; set; }
            public List<HeatmapPointDto> Points { get; set; } = new();
        }

        public class CompareDaysResponse
        {
            public int? RestaurantId { get; set; }
            public DateTime DayA { get; set; }
            public DateTime DayB { get; set; }
            public List<HourlyPointDto> DayAHourly { get; set; } = new();
            public List<HourlyPointDto> DayBHourly { get; set; } = new();
        }
    }
}
