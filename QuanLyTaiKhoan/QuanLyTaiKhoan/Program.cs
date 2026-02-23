using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using QuanLyTaiKhoan.Models;

namespace QuanLyTaiKhoan
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Lấy chuỗi kết nối trước
            var connectionString = builder.Configuration.GetConnectionString("TmdtConnection");

            // *** SỬA LỖI 1: Đảm bảo kiểm tra Connection String trước khi AddDbContext ***

            if (string.IsNullOrEmpty(connectionString))
            {
                // Thay vì ném lỗi, hãy in ra log cảnh báo hoặc đảm bảo nó được cấu hình trong appsettings.json
                // Tuy nhiên, ném lỗi là cách tốt để đảm bảo ứng dụng không chạy khi không kết nối được DB.
                throw new InvalidOperationException("Chuỗi kết nối 'TmdtConnection' chưa được cấu hình hoặc rỗng. Vui lòng kiểm tra appsettings.json.");
            }

            // 1. Đăng ký DbContext (TmdtContext) - CHỈ GỌI MỘT LẦN
            builder.Services.AddDbContext<TmdtContext>(options =>
                options.UseSqlServer(connectionString));

            // --- 2. Cấu hình Cookie Authentication và Authorization ---

            // Đăng ký Authentication Scheme mặc định (Cookie)
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.Cookie.Name = "TMDT_Auth"; // Tên Cookie xác thực
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(30); // Cookie sống 30 phút
                    options.SlidingExpiration = true; // Làm mới thời gian sống nếu người dùng hoạt động
                    options.Cookie.HttpOnly = true; // Bảo mật: Không thể truy cập từ client script (JS)
                    options.Cookie.IsEssential = true;
                    // Nếu dùng cho API, bạn có thể thiết lập sự kiện trả về mã lỗi 401 Unauthorized thay vì redirect
                    options.Events.OnRedirectToLogin = context =>
                    {             
                        context.Response.Redirect(context.RedirectUri);
                        return Task.CompletedTask;
                    };
                });

            // Đăng ký Authorization
            builder.Services.AddAuthorization();

            // --- 3. Đăng ký các Dịch vụ Hệ thống và HttpClient ---

            // Tối ưu hóa: Chỉ cần AddHttpClient()
            builder.Services.AddHttpClient();

            builder.Services.AddMemoryCache(); // Đăng ký IMemoryCache
            builder.Services.AddHttpContextAccessor(); // Đăng ký IHttpContextAccessor

            // Add services to the container.
            builder.Services.AddControllersWithViews();
            builder.Services.AddControllers(); // Hỗ trợ API Controllers

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                // Trong môi trường Development, có thể muốn dùng DeveloperExceptionPage
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // Trong môi trường Production
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            // --- VỊ TRÍ MIDDLEWARE XÁC THỰC RẤT CHUẨN ---
            // Phải đặt UseAuthentication trước UseAuthorization
            app.UseAuthentication();
            app.UseAuthorization();
            // --- KẾT THÚC VỊ TRÍ CHUẨN ---

            // 4. Định tuyến Endpoint
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.MapControllers(); // Định tuyến cho API Controller

            app.Run();
        }

        
    }
}