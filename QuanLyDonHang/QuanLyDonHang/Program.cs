using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using QuanLyDonHang;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using QuanLyDonHang.Models;

var builder = WebApplication.CreateBuilder(args);
// Thêm dòng này để đăng ký IHttpClientFactory
builder.Services.AddHttpClient();

// Nếu bạn sử dụng "BypassSSL" như trong code trước đó, hãy đăng ký cụ thể:
builder.Services.AddHttpClient("BypassSSL", client => {
    // Cấu hình mặc định nếu cần
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
});
// 1. Cấu hình DbContext
builder.Services.AddDbContext<TmdtContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Cấu hình CORS (PHẢI THÊM ĐOẠN NÀY)
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
builder.Services.AddMemoryCache();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<RabbitMQProducer>();
var app = builder.Build();

// THỨ TỰ MIDDLEWARE (QUAN TRỌNG)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 3. CORS phải nằm SAU Redirection và TRƯỚC Authorization/MapControllers
app.UseRouting();
app.UseCors("AllowSpecificOrigins");

// Session phải nằm trước Authentication
app.UseSession();

app.UseAuthorization();

app.MapControllers();

app.Run();