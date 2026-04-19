using FoodStreetWeb.Data;
using FoodStreetWeb.Hubs;
using FoodStreetWeb.Models;
using FoodStreetWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using CloudinaryDotNet;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// MVC + API JSON options
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddHttpClient<TranslateService>();
builder.Services.AddSignalR();

builder.Services.AddSingleton(_ =>
{
    var cloudName = builder.Configuration["Cloudinary:CloudName"];
    var apiKey = builder.Configuration["Cloudinary:ApiKey"];
    var apiSecret = builder.Configuration["Cloudinary:ApiSecret"];

    var account = new Account(cloudName, apiKey, apiSecret);
    var cloudinary = new Cloudinary(account);
    cloudinary.Api.Secure = true;
    return cloudinary;
});

// MySQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(
            builder.Configuration.GetConnectionString("DefaultConnection")
        )
    ));

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<OnlineUsersService>();
builder.Services.AddSingleton<OnlineDeviceStore>();

// Cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
});

// Cấu hình localization (chỉ để đọc cookie và thiết lập CultureInfo)
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("vi-VN"),
        new CultureInfo("en-US"),
        new CultureInfo("zh-CN")
    };
    options.DefaultRequestCulture = new RequestCulture("vi-VN");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});

var app = builder.Build();

// Error handling
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseMiddleware<CookieTrackingMiddleware>();

app.Use(async (context, next) =>
{
    var cultureCookie = context.Request.Cookies["ASPNETCORE_CULTURE"];
    if (!string.IsNullOrEmpty(cultureCookie))
    {
        try
        {
            var culture = new CultureInfo(cultureCookie); // "vi-VN", "en-US", "zh-CN"
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
        catch { /* fallback nếu cookie không hợp lệ */ }
    }
    await next();
});

//var locOptions = app.Services.GetService<IOptions<RequestLocalizationOptions>>();
//app.UseRequestLocalization(locOptions.Value);

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();
app.MapHub<DevicePresenceHub>("/hubs/device-presence");

// Tạo Role mặc định
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS `ScanLogs` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `DeviceId` longtext NOT NULL,
    `RestaurantId` int NOT NULL,
    `ScanTime` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`)
);");

    await dbContext.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS `OnlineWebPresences` (
    `PresenceId` varchar(255) NOT NULL,
    `VisitorId` varchar(191) NOT NULL,
    `DeviceId` varchar(191) NOT NULL,
    `TabId` varchar(191) NOT NULL,
    `RestaurantId` int NOT NULL,
    `Role` longtext NOT NULL,
    `IsFromQr` tinyint(1) NOT NULL,
    `LastPath` longtext NOT NULL,
    `LastSeenUtc` datetime(6) NOT NULL,
    PRIMARY KEY (`PresenceId`),
    INDEX `IX_OnlineWebPresences_LastSeenUtc` (`LastSeenUtc`),
    INDEX `IX_OnlineWebPresences_Restaurant_Device_Seen` (`RestaurantId`, `DeviceId`, `LastSeenUtc`)
);");

    await dbContext.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS `DeviceConnectionHistories` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `DeviceId` varchar(191) NOT NULL,
    `ConnectionId` varchar(191) NOT NULL,
    `EventType` varchar(32) NOT NULL,
    `EventTimeUtc` datetime(6) NOT NULL,
    `Note` longtext NULL,
    PRIMARY KEY (`Id`),
    INDEX `IX_DeviceConnectionHistories_EventTimeUtc` (`EventTimeUtc`),
    INDEX `IX_DeviceConnectionHistories_Device_EventTime` (`DeviceId`, `EventTimeUtc`)
);");

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    string[] roles = { "Admin", "RestaurantOwner" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }
}

app.Run();