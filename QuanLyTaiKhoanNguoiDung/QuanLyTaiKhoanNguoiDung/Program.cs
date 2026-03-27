using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using QuanLyTaiKhoanNguoiDung.BackgroundTasks;
using QuanLyTaiKhoanNguoiDung.Models12;
using QuanLyTaiKhoanNguoiDung.Models12._1234;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyLichLamViec;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNhatKyHeThong;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyPhanQuyen;
using Tmdt.Shared.Services;

namespace QuanLyTaiKhoanNguoiDung
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. Cấu hình Cơ sở dữ liệu
            var connectionString = builder.Configuration.GetConnectionString("TmdtConnection");
            builder.Services.AddDbContext<TmdtContext>(options =>
                options.UseSqlServer(connectionString));

            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(@"C:\SharedKeys\TMDT_Auth")) // Đường dẫn chung trên máy chủ
                .SetApplicationName("TMDT_System_Shared");

            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options => {
                    options.Cookie.Name = "TMDT_Shared_Auth";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.Path = "/";

                    // QUAN TRỌNG: Trên localhost khác port, không được set Domain.
                    options.Cookie.Domain = null;

                    // Thử chuyển SameSite về Lax để trình duyệt dễ chấp nhận hơn khi chạy local
                    options.Cookie.SameSite = SameSiteMode.Lax;

                    // Đảm bảo SecurePolicy đồng nhất
                    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

                    options.ExpireTimeSpan = TimeSpan.FromHours(8);
                    options.SlidingExpiration = true;
                });
            builder.Services.AddSession(options => {
                options.Cookie.Name = ".TMDT.Session";
                options.IdleTimeout = TimeSpan.FromHours(20);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                // Đổi về Lax và SameAsRequest để chạy mượt trên localhost
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            });
           
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = "localhost:6379"; // Địa chỉ Redis server của bạn
                options.InstanceName = "TMDT_Session_";
            });
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigins", corsBuilder =>
                {
                    corsBuilder.WithOrigins("https://localhost:7022", "https://localhost:7286", "https://localhost:7264", "https://localhost:7097", "https://localhost:7088")
                               .AllowAnyHeader()
                               .AllowAnyMethod()
                               .AllowCredentials();

                });
            });

            // 5. Các dịch vụ khác
            builder.Services.AddHttpClient("BypassSSL").ConfigurePrimaryHttpMessageHandler(() => {
                return new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
                };
            });

            builder.Services.AddAuthorization();
            builder.Services.AddMemoryCache();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddControllersWithViews();
            builder.Services.AddControllers();

            builder.Services.AddScoped<IEmailService, EmailService>();
            builder.Services.AddHostedService<LicenseExpiryWorker>();
            builder.Services.AddHostedService<AutoApprovalService>();
           
            builder.Services.AddScoped<PhanQuyenService>();
           
            builder.Services.AddSingleton<RabbitMQClient>(); 
            builder.Services.AddSingleton<CacheSignalService>();
            builder.Services.AddScoped<ISystemService, SystemService>(); 
            builder.Services.AddHttpContextAccessor(); 
            OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            var app = builder.Build();

            // 6. Thứ tự Middleware (CỰC KỲ QUAN TRỌNG)
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseCors("AllowSpecificOrigins");

            // THỨ TỰ: Session -> Auth
            app.UseSession();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=QuanLyPhanQuyen}/{action=DangNhap}");

            app.MapControllers();
            app.Run();
        }
    }
}