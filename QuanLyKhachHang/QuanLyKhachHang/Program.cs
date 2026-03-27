using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using QuanLyKhachHang.Models1.QuanLyMucDoDichVu;
using Tmdt.Shared.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình Cơ sở dữ liệu
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<QuanLyKhachHang.TmdtContext>(options =>
    options.UseSqlServer(connectionString));

// 2. Cấu hình CORS (Đảm bảo các Port localhost có thể gọi nhau)
builder.Services.AddCors(options => {
    options.AddPolicy("AllowSpecificOrigins", corsBuilder => {
        corsBuilder.WithOrigins("https://localhost:7022", "https://localhost:7149")
                   .AllowAnyHeader()
                   .AllowAnyMethod()
                   .AllowCredentials(); // Bắt buộc để truyền Cookie/Auth
    });
});

// 3. CHIA SẺ CHÌA KHÓA MÃ HÓA (Data Protection)
// Giúp App này giải mã được Cookie mà App Tài khoản tạo ra
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(@"C:\SharedKeys\TMDT_Auth"))
    .SetApplicationName("TMDT_System_Shared");

// 4. CẤU HÌNH XÁC THỰC (Authentication) - Dùng chung Cookie
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => {
        options.Cookie.Name = "TMDT_Shared_Auth"; // Tên Cookie phải giống hệt ở App Tài khoản
                                                  // 1. Chỉ để đường dẫn ảo trong App hiện tại để tránh lỗi Exception
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";

        // 2. CẤU HÌNH QUAN TRỌNG: Điều hướng sang App Tài khoản (Port 7022)
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                // Kiểm tra nếu là yêu cầu API (bắt đầu bằng /api)
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                }
                else
                {
                    // Nếu là trang web bình thường thì mới Redirect
                    string loginUrl = "https://localhost:7022/QuanLyPhanQuyen/DangNhap";
                    context.Response.Redirect(loginUrl + "?ReturnUrl=" + context.Request.Path);
                }
                return Task.CompletedTask;
            }
        };

        options.Cookie.HttpOnly = true;
        options.Cookie.Path = "/";
        options.Cookie.Domain = null; // Chạy localhost khác Port thì để null
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

// 5. CẤU HÌNH PHÂN QUYỀN (Authorization)
builder.Services.AddAuthorization();

// 6. CẤU HÌNH SESSION QUA REDIS
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
    options.InstanceName = "TMDT_Session_"; // Giúp phân biệt các Session của hệ thống TMDT
});

builder.Services.AddSession(options => {
    options.Cookie.Name = ".TMDT.Session";
    options.IdleTimeout = TimeSpan.FromHours(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// 7. ĐĂNG KÝ CÁC DỊCH VỤ HỆ THỐNG
builder.Services.AddHttpContextAccessor(); // Cực kỳ quan trọng để lấy thông tin User trong Service
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<RabbitMQClient>();
builder.Services.AddSingleton<CacheSignalService>();
builder.Services.AddHttpClient<IFlightService, AmadeusFlightService>();
builder.Services.AddScoped<ISystemService, SystemService>();

// 8. CẤU HÌNH CONTROLLER & SWAGGER
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 9. Cấu hình HttpClient để bỏ qua lỗi SSL khi gọi API nội bộ (nếu cần)
builder.Services.AddHttpClient("BypassSSL").ConfigurePrimaryHttpMessageHandler(() => {
    return new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
    };
});

var app = builder.Build();

// --- THỨ TỰ MIDDLEWARE (QUAN TRỌNG NHẤT) ---

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Thêm cái này nếu App có dùng file tĩnh
app.UseRouting();

// CORS phải đứng trước Auth
app.UseCors("AllowSpecificOrigins");

// THỨ TỰ: Session -> Authentication -> Authorization
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();