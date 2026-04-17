using FoodStreetWeb.Services;

public class CookieTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private const string VisitorCookieName = "VisitorId";
    private const string QrCookieName = "FromQrVisitor";

    public CookieTrackingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, OnlineUsersService onlineService)
    {
        string visitorId = context.Request.Cookies[VisitorCookieName];
        if (string.IsNullOrEmpty(visitorId))
        {
            visitorId = Guid.NewGuid().ToString();
            context.Response.Cookies.Append(VisitorCookieName, visitorId, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(30),
                IsEssential = true
            });
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
        onlineService.UpdateUser(visitorId, role, isFromQr, context.Request.Path);
        await _next(context);
    }
}