using Microsoft.EntityFrameworkCore;
using QuanLyTaiKhoanNguoiDung.Models;
using QuanLyTaiKhoanNguoiDung.Services;

namespace QuanLyTaiKhoanNguoiDung.BackgroundTasks
{
    public class AutoApprovalService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<AutoApprovalService> _logger;

        public AutoApprovalService(IServiceProvider services, ILogger<AutoApprovalService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                // Kiểm tra đúng ngày 1 hoặc 15 lúc nửa đêm
                if ((now.Day == 1 || now.Day == 15) && now.Hour == 0 && now.Minute == 1)
                {
                    await PerformAutoApproval();
                    // Đợi 1 tiếng để không bị lặp lại trong cùng 1 phút
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        private async Task PerformAutoApproval()
        {
            using (var scope = _services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TmdtContext>();
                var aiService = scope.ServiceProvider.GetRequiredService<IAISchedulingService>();

                // 1. Lấy danh sách chờ duyệt kèm đầy đủ thông tin để chấm điểm
                var pendingList = await context.DangKyCaTrucs
                    .Include(x => x.MaCaNavigation)
                    .Include(x => x.MaNguoiDungNavigation).ThenInclude(nd => nd.TaiXe)
                    .Where(x => x.TrangThai == "Chờ duyệt")
                    .ToListAsync();

                if (!pendingList.Any()) return;

                // 2. Lấy lịch sử để AI so sánh
                var userIds = pendingList.Select(x => x.MaNguoiDung).Distinct().ToList();
                var history = await context.DangKyCaTrucs
                    .Where(x => userIds.Contains(x.MaNguoiDung) && x.TrangThai == "Đã duyệt")
                    .ToListAsync();

                // 3. Duyệt theo từng cụm (Ngày - Ca - Kho)
                var groups = pendingList.GroupBy(x => new { x.NgayTruc, x.MaCa, x.MaNguoiDungNavigation.MaKho });

                foreach (var g in groups)
                {
                    var scoredItems = g.Select(dk => aiService.AnalyzeShift(dk, history.Where(h => h.MaNguoiDung == dk.MaNguoiDung).ToList()))
                                      .OrderByDescending(x => x.AI_Score)
                                      .Take(3) // Định mức mỗi ca 3 người
                                      .ToList();

                    foreach (var item in scoredItems)
                    {
                        var record = pendingList.First(x => x.MaDangKy == item.MaDangKy);
                        record.TrangThai = "Đã duyệt";
                    }
                }

                await context.SaveChangesAsync();
                _logger.LogInformation($"[AI] Đã tự động duyệt thành công lúc {DateTime.Now}");
            }
        }
    }
}