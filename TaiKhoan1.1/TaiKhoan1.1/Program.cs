using Microsoft.EntityFrameworkCore;
using TaiKhoan1._1.Model11; // Đảm bảo namespace này chứa TmdtContext
using Microsoft.AspNetCore.Authentication.Cookies; // Thêm using này
using System.Security.Claims;
using TaiKhoan1.Models; // Thêm using này

namespace TaiKhoan1._1
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. Đăng ký DbContext (TmdtContext)
            var connectionString = builder.Configuration.GetConnectionString("TmdtConnection");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Chuỗi kết nối 'TmdtConnection' chưa được cấu hình.");
            }

            builder.Services.AddDbContext<TmdtContext>(options =>
                options.UseSqlServer(connectionString));

            // --- BỔ SUNG: 2. Cấu hình Cookie Authentication và Authorization ---

            // Đăng ký Authentication Scheme mặc định (Cookie)
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.Cookie.Name = "TMDT_Auth"; // Tên Cookie xác thực
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(30); // Cookie sống 30 phút
                    options.SlidingExpiration = true; // Làm mới thời gian sống nếu người dùng hoạt động

                    // Tuy nhiên, đối với API, thường không cần định tuyến lại (Redirect)
                    // Nếu dùng cho MVC, bạn sẽ thêm:
                    // options.LoginPath = "/Home/Login";
                    options.Cookie.HttpOnly = true; // Bảo mật: Không thể truy cập từ client script (JS)
                    options.Cookie.IsEssential = true;
                });

            // Đăng ký Authorization
            builder.Services.AddAuthorization();

            // --- HẾT BỔ SUNG AUTH ---


            // 3. Đăng ký các Dịch vụ Hệ thống và HttpClient

            // Tối ưu hóa: Chỉ cần AddHttpClient(), không cần AddSingleton<HttpClient>
            builder.Services.AddHttpClient();

            builder.Services.AddMemoryCache(); // Đăng ký IMemoryCache
            builder.Services.AddHttpContextAccessor(); // Đăng ký IHttpContextAccessor

            // Add services to the container.
            builder.Services.AddControllersWithViews();
            builder.Services.AddControllers(); // Hỗ trợ API Controllers

            var app = builder.Build();

            
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            // --- THAY ĐỔI VỊ TRÍ VÀ THÊM MIDDLEWARE XÁC THỰC ---
            // Phải đặt UseAuthentication trước UseAuthorization

            app.UseAuthentication();
            app.UseAuthorization();

            // --- HẾT THAY ĐỔI ---

            // 4. Định tuyến Endpoint
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.MapControllers();

            app.Run();
        }
    }
}