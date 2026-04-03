using Microsoft.EntityFrameworkCore;
using QuanLyLoTrinhTheoDoi.Models;
using QuanLyLoTrinhTheoDoi;
// Thêm namespace chứa class Consumer của bạn (nếu khác namespace hiện tại)
// using QuanLyLoTrinhTheoDoi.Services; 

var builder = WebApplication.CreateBuilder(args);

// --- 1. ĐĂNG KÝ HTTP CLIENT ---
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("PhuongTienApi", c => c.BaseAddress = new Uri("https://localhost:7286/"));
builder.Services.AddHttpClient("KhoApi", c => c.BaseAddress = new Uri("https://localhost:7286/"));
builder.Services.AddHttpClient("NhanSuApi", c => c.BaseAddress = new Uri("https://localhost:7022/"));
builder.Services.AddHttpClient("DonHangApi", c => c.BaseAddress = new Uri("https://localhost:7264/"));

// --- 2. ĐĂNG KÝ CƠ SỞ DỮ LIỆU ---
builder.Services.AddDbContext<TmdtContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- 3. CÁC DỊCH VỤ HỆ THỐNG KHÁC ---
builder.Services.AddControllers()
    .AddJsonOptions(options => {
        // Giúp xử lý JSON mượt hơn, tránh lỗi vòng lặp hoặc đặt tên theo chuẩn Pascal
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

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

// --- 5. ĐĂNG KÝ BACKGROUND SERVICE (Kích hoạt RabbitMQ Consumer) ---
// Đăng ký class RoutingOrderConsumer để nó tự động chạy khi bật Server
builder.Services.AddHostedService<RoutingOrderConsumer>();

// Nếu bạn có class Producer ở server này để gửi ngược lại phản hồi:
// builder.Services.AddScoped<IRabbitMQProducer, RabbitMQProducer>();

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