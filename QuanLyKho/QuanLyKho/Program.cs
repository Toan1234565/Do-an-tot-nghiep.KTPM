using Microsoft.EntityFrameworkCore;
using QuanLyKho;
using QuanLyKho.Models;
using OfficeOpenXml;

var builder = WebApplication.CreateBuilder(args);

// --- 1. CẤU HÌNH SERVICES (Dependency Injection) ---

// Cấu hình DbContext
builder.Services.AddDbContext<TmdtContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("TmdtConnection")));

// Cấu hình CORS (Chỉnh đúng URL của Front-end)
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

builder.Services.AddMemoryCache();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- 2. CẤU HÌNH LICENSE EPPLUS (Sửa lỗi triệt để) ---
// Sử dụng cú pháp chuẩn cho EPPlus 5, 6, 7 và 8
OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

var app = builder.Build();

// --- 3. CẤU HÌNH HTTP REQUEST PIPELINE (Middleware) ---

// 1. Swagger luôn nằm đầu tiên trong môi trường Dev
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 2. Các middleware cơ bản
app.UseHttpsRedirection();
app.UseStaticFiles();

// 3. Routing (Phải nằm TRƯỚC Cors và Authorization)
app.UseRouting();

// 4. CORS (Phải nằm SAU UseRouting và TRƯỚC UseAuthorization)
app.UseCors("AllowSpecificOrigins");

// 5. Auth
app.UseAuthorization();

// 6. Map Controllers
app.MapControllers();

app.Run();