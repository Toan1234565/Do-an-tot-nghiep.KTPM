using Microsoft.EntityFrameworkCore;
using QuanLyKho;
using QuanLyKho.Models;
using OfficeOpenXml;

var builder = WebApplication.CreateBuilder(args);

// --- 1. CẤU HÌNH SERVICES ---
builder.Services.AddDbContext<TmdtContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("TmdtConnection")));

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

// QUAN TRỌNG: Đăng ký HttpClient để fix lỗi InvalidOperationException
builder.Services.AddHttpClient();

// --- 2. CẤU HÌNH LICENSE EPPLUS ---
OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

var app = builder.Build();

// --- 3. CẤU HÌNH MIDDLEWARE ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowSpecificOrigins");
app.UseAuthorization();
app.MapControllers();

app.Run();