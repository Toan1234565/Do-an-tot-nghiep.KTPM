using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

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

            // 2. Cấu hình CORS (Cho phép gọi từ Web đến API)
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

            // 3. Cấu hình Authentication & Cookie
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.Cookie.Name = "TMDT_Auth";
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                    options.SlidingExpiration = true;
                    options.Cookie.HttpOnly = true;
                });

            // 4. Cấu hình HttpClient Bypass SSL (Dùng để gọi API localhost)
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
            builder.Services.AddMemoryCache();

            var app = builder.Build(); // --- XÂY DỰNG APP TẠI ĐÂY ---

            // 5. Thứ tự Middleware (CỰC KỲ QUAN TRỌNG)
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();

            app.UseCors("AllowSpecificOrigins"); // CORS phải đặt trước Auth

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.MapControllers();
            app.Run();
        }
    }
}