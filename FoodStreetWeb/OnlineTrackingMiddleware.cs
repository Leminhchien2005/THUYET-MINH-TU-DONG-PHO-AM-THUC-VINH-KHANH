using FoodStreetWeb.Services;

public class CookieTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private const string VisitorCookieName = "VisitorId";
    private const string DeviceCookieName = "DeviceId";
    private const string QrCookieName = "FromQrVisitor";
    private const string DeviceHeaderName = "X-Device-Id";
    private const string TabHeaderName = "X-Tab-Id";

    public CookieTrackingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, OnlineUsersService onlineService)
    {
        var visitorId = GetOrCreateCookie(context, VisitorCookieName, 30);
        var deviceId = context.Request.Headers[DeviceHeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            deviceId = GetOrCreateCookie(context, DeviceCookieName, 90);
        }

        var tabId = context.Request.Headers[TabHeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(tabId))
        {
            tabId = "legacy";
        }

        var role = "Du khách";
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            if (context.User.IsInRole("Admin"))
                role = "Admin";
            else if (context.User.IsInRole("RestaurantOwner"))
                role = "Nhà hàng";
        }

        var isFromQr = context.Request.Cookies[QrCookieName] == "1";
        await onlineService.UpdateUserAsync(visitorId, deviceId, tabId, role, isFromQr, context.Request.Path.Value);
        await _next(context);
    }

    private static string GetOrCreateCookie(HttpContext context, string name, int expiresInDays)
    {
        var value = context.Request.Cookies[name];
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        value = Guid.NewGuid().ToString("N");
        context.Response.Cookies.Append(name, value, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(expiresInDays),
            IsEssential = true
        });

        return value;
    }
}
