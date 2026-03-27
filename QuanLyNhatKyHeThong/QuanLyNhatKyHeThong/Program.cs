using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text;
using QuanLyNhatKyHeThong.Models12;
using QuanLyTaiKhoanNguoiDung.BackgroundServices;

namespace QuanLyNhatKyHeThong
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // --- 1. CẤU HÌNH DỊCH VỤ (SERVICES) ---

            // DbContext
            builder.Services.AddDbContext<TmdtContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Hỗ trợ Controller, Cache và HttpClient
            builder.Services.AddControllers();
            builder.Services.AddMemoryCache();
            builder.Services.AddHttpClient();

            // Cấu hình CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowWebProject", policy =>
                {
                    policy.WithOrigins("https://localhost:7022")
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                });
            });

            // Cấu hình Authentication (JWT Bearer)
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        // Lưu ý: Key này phải khớp với bên Server Auth
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("Chuoi_Secret_Key_Cua_Ban_O_Day"))
                    };
                });

            // Đăng ký Worker chạy ngầm
            builder.Services.AddHostedService<LogConsumer>();

            // Swagger/OpenAPI
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // --- 2. CẤU HÌNH PIPELINE (MIDDLEWARE) ---

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            // Thứ tự Middleware cực kỳ quan trọng:
            app.UseRouting();

            // 1. CORS phải đặt trước Auth
            app.UseCors("AllowWebProject");

            // 2. Authentication (Ai là người truy cập?)
            app.UseAuthentication();

            // 3. Authorization (Người đó có quyền làm gì?)
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}