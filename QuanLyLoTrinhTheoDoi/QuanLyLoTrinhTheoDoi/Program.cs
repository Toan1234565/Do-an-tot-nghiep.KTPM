using Microsoft.EntityFrameworkCore;
using QuanLyLoTrinhTheoDoi.Models;
using QuanLyLoTrinhTheoDoi;

var builder = WebApplication.CreateBuilder(args);

// --- 1. ĐĂNG KÝ HTTP CLIENT (Giải quyết lỗi resolve IHttpClientFactory) ---
builder.Services.AddHttpClient(); // Cực kỳ quan trọng

// Cấu hình Named Clients để gọi API từ các Microservices khác
builder.Services.AddHttpClient("PhuongTienApi", c => c.BaseAddress = new Uri("https://localhost:7286/"));
builder.Services.AddHttpClient("KhoApi", c => c.BaseAddress = new Uri("https://localhost:7286/"));
builder.Services.AddHttpClient("NhanSuApi", c => c.BaseAddress = new Uri("https://localhost:7022/"));
builder.Services.AddHttpClient("DonHangApi", c => c.BaseAddress = new Uri("https://localhost:7264/"));
// --- 2. ĐĂNG KÝ CƠ SỞ DỮ LIỆU ---
builder.Services.AddDbContext<TmdtContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- 3. CÁC DỊCH VỤ HỆ THỐNG KHÁC ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

// --- 4. CẤU HÌNH CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", corsBuilder =>
    {
        corsBuilder.WithOrigins("https://localhost:7022")
                   .AllowAnyHeader()
                   .AllowAnyMethod()
                   .AllowCredentials();
    });
});

// --- 5. ĐĂNG KÝ BACKGROUND SERVICE ---
builder.Services.AddHostedService<OrderTaskConsumer>(); //

var app = builder.Build();

// --- 6. CẤU HÌNH MIDDLEWARE ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowSpecificOrigins");
app.UseAuthorization();
app.MapControllers();

app.Run();