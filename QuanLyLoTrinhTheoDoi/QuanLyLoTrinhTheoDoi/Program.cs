using Microsoft.EntityFrameworkCore;
using QuanLyLoTrinhTheoDoi.Models;
using QuanLyLoTrinhTheoDoi;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình DbContext
builder.Services.AddDbContext<TmdtContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Cấu hình CORS (PHẢI THÊM ĐOẠN NÀY)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", corsBuilder =>
    {
        corsBuilder.WithOrigins("https://localhost:7022") // URL của project Web
                   .AllowAnyHeader()
                   .AllowAnyMethod()
                   .AllowCredentials();
    });
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// THỨ TỰ MIDDLEWARE (QUAN TRỌNG)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 3. CORS phải nằm SAU Redirection và TRƯỚC Authorization/MapControllers
app.UseRouting();
app.UseCors("AllowSpecificOrigins");

app.UseAuthorization();

app.MapControllers();

app.Run();