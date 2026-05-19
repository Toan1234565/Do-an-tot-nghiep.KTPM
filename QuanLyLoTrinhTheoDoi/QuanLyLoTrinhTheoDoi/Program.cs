using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using QuanLyLoTrinhTheoDoi;
using QuanLyLoTrinhTheoDoi.Models;
using QuanLyLoTrinhTheoDoi.Models12.LienServer;
using QuanLyLoTrinhTheoDoi.Models12.LienServer.cs;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer;
using Tmdt.Shared.Services;

var builder = WebApplication.CreateBuilder(args);

// --- 1. CẤU HÌNH CORS (Chỉ khai báo 1 lần duy nhất) ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", corsBuilder =>
    {
        corsBuilder.WithOrigins("https://localhost:7022") // URL của project Web
                   .AllowAnyHeader()
                   .AllowAnyMethod()
                   .AllowCredentials();
    });
});

// --- 2. CHIA SẺ CHÌA KHÓA MÃ HÓA (Data Protection) ---
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(@"C:\SharedKeys\TMDT_Auth"))
    .SetApplicationName("TMDT_System_Shared");

// --- 3. CẤU HÌNH XÁC THỰC COOKIE CHIA SẺ ---
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

// --- 4. ĐĂNG KÝ CÁC HTTP CLIENT ---
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("PhuongTienApi", c => c.BaseAddress = new Uri("https://localhost:7286/"));
builder.Services.AddHttpClient("KhoApi", c => c.BaseAddress = new Uri("https://localhost:7286/"));
builder.Services.AddHttpClient("NhanSuApi", c => c.BaseAddress = new Uri("https://localhost:7022/"));
builder.Services.AddHttpClient("DonHangApi", c => c.BaseAddress = new Uri("https://localhost:7264/"));
builder.Services.AddHttpClient("KhachHangApi", client =>
{
    client.BaseAddress = new Uri("https://localhost:7149/");
});

// --- 5. ĐĂNG KÝ CƠ SỞ DỮ LIỆU ---
builder.Services.AddDbContext<TmdtContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- 6. CÁC DỊCH VỤ HỆ THỐNG (CONTROLLERS & SWAGGER) ---
builder.Services.AddControllers()
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

// --- 7. CẤU HÌNH SESSION & REDIS CACHE ---
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

// --- 8. ĐĂNG KÝ DEPENDENCY INJECTION (DI) ---

// Các Dịch vụ hạ tầng dùng chung toàn hệ thống (Khai báo trước để SystemService gọi vào)
builder.Services.AddSingleton<RabbitMQClient>();
builder.Services.AddSingleton<CacheSignalService>();

// Đăng ký SystemService dạng Scoped để phục vụ lấy thông tin theo phiên Request
builder.Services.AddScoped<ISystemService, SystemService>();

// Đăng ký các Client Service kết nối API nội bộ
builder.Services.AddScoped<INhanVienService, ThongTinNhanVienClient>();
builder.Services.AddScoped<IDiaChiService, DiaChiServiceClient>();
builder.Services.AddScoped<IDonHangService, DonHangServiceClient>();
builder.Services.AddScoped<IKhachHangServiceClient, ChiTietKhachHangServerClient>();
builder.Services.AddScoped<IPhuongTienServiceClient, PhuongTienServiceClient>();
builder.Services.AddScoped<IKhoBaiService, KhoBaiSevicrClient>();
builder.Services.AddScoped<IPhuongTienTaiXeService, PhuongTienTaiXeService>();

// --- 9. ĐĂNG KÝ BACKGROUND SERVICE (RabbitMQ Consumer) ---
builder.Services.AddHostedService<RoutingOrderConsumer>();

var app = builder.Build();

// --- 10. CẤU HÌNH MIDDLEWARE PIPELINE (Thứ tự chuẩn hóa để sửa lỗi Session) ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Khởi tạo Routing đầu tiên
app.UseRouting();

// Chặn CORS ngay sau khi xác định được Route
app.UseCors("AllowSpecificOrigins");

// BẮT BUỘC: Kích hoạt Session trước khi chạy Authentication/Authorization và gọi vào API
app.UseSession();

// Kích hoạt Xác thực cookie chia sẻ và phân quyền
app.UseAuthentication();
app.UseAuthorization();

// Map route vào các API Controller cụ thể
app.MapControllers();

app.Run();