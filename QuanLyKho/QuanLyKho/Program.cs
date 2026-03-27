using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using QuanLyKho;
using QuanLyKho.Models;
using Tmdt.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.StackExchangeRedis; 

var builder = WebApplication.CreateBuilder(args);

// --- 1. CẤU HÌNH DB & CƠ BẢN ---
builder.Services.AddDbContext<TmdtContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("TmdtConnection")));

builder.Services.AddControllersWithViews(); // Hỗ trợ cả API và View nếu cần
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

// --- 2. CẤU HÌNH CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", corsBuilder =>
    {
        corsBuilder.WithOrigins("https://localhost:7022") // Port của App Tài Khoản
                   .AllowAnyHeader()
                   .AllowAnyMethod()
                   .AllowCredentials();
    });
});

// --- 3. CHIA SẺ CHÌA KHÓA MÃ HÓA (Data Protection) ---
// Đảm bảo thư mục C:\SharedKeys\TMDT_Auth đã được tạo và có quyền ghi
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(@"C:\SharedKeys\TMDT_Auth"))
    .SetApplicationName("TMDT_System_Shared");

// --- 4. CẤU HÌNH XÁC THỰC (Shared Cookie) ---
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => {
        options.Cookie.Name = "TMDT_Shared_Auth";
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";

        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                }
                else
                {
                    // Redirect sang App Tài khoản (Port 7022)
                    string loginUrl = "https://localhost:7022/QuanLyPhanQuyen/DangNhap";
                    context.Response.Redirect(loginUrl + "?ReturnUrl=" + context.Request.Path);
                }
                return Task.CompletedTask;
            }
        };

        options.Cookie.HttpOnly = true;
        options.Cookie.Path = "/";
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

// --- 5. CẤU HÌNH SESSION & REDIS ---
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetSection("Redis:ConnectionString").Value ?? "localhost:6379";
    options.InstanceName = "TMDT_Session_";
});

builder.Services.AddSession(options => {
    options.Cookie.Name = ".TMDT.Session";
    options.IdleTimeout = TimeSpan.FromHours(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// --- 6. ĐĂNG KÝ SERVICES HỆ THỐNG ---
builder.Services.AddSingleton<RabbitMQClient>();
builder.Services.AddSingleton<CacheSignalService>();
builder.Services.AddScoped<ISystemService, SystemService>();

// Cấu hình License EPPLUS
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

var app = builder.Build();

// --- 7. CẤU HÌNH MIDDLEWARE (Thứ tự cực kỳ quan trọng) ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// CORS phải nằm sau UseRouting và trước UseAuthentication
app.UseCors("AllowSpecificOrigins");

// Session phải nằm trước Authentication
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// Map Routes
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();