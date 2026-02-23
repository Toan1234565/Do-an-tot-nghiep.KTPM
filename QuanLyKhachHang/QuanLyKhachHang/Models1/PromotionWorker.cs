using Microsoft.EntityFrameworkCore;

namespace QuanLyKhachHang.Models1
{
    public class PromotionWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PromotionWorker> _logger;

        public PromotionWorker(IServiceProvider serviceProvider, ILogger<PromotionWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Đang quét các khuyến mãi hết hạn lúc: {time}", DateTimeOffset.Now);

                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<TmdtContext>(); // Thay bằng DbContext của bạn

                    // Tìm các khuyến mãi đang chạy nhưng đã quá ngày kết thúc
                    var expiredPromotions = await context.KhuyenMais
                        .Where(k => k.TrangThai == true && k.NgayKetThuc < DateTime.Now)
                        .ToListAsync();

                    if (expiredPromotions.Any())
                    {
                        foreach (var item in expiredPromotions)
                        {
                            item.TrangThai = false;
                            _logger.LogInformation("Đã tự động vô hiệu hóa KM: {Ten}", item.TenChuongTrinh);
                        }
                        await context.SaveChangesAsync();

                        // Xóa cache (nếu bạn dùng MemoryCache hoặc Redis)
                        // ClearPriceRegionCache(); 
                    }
                }

                // Chờ 1 tiếng sau quét lại (60 phút * 60 giây * 1000 ms)
                await Task.Delay(3600000, stoppingToken);
            }
        }
    }
}
