using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using QuanLyTaiKhoanNguoiDung.Models12._1234;
using OfficeOpenXml;

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

            // 2. Cấu hình Authentication & Cookie
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.Cookie.Name = "TMDT_Auth";
                    options.LoginPath = "/QuanLyPhanQuyen/DangNhap";
                    options.AccessDeniedPath = "/Home/Error";
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
                    options.SlidingExpiration = true;
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                    options.Cookie.SameSite = SameSiteMode.Lax;
                });

            // 3. Cấu hình Session (SỬA LỖI TẠI ĐÂY)
            builder.Services.AddDistributedMemoryCache(); // Cần thiết cho Session
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true; // Bắt buộc để hoạt động
            });

            // 4. Cấu hình CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigins", corsBuilder =>
                {
                    corsBuilder.WithOrigins("https://localhost:7022", "https://localhost:7286", "https://localhost:7264", "https://localhost:7097")
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