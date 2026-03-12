using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using QuanLyTaiKhoanNguoiDung.Models;

namespace QuanLyTaiKhoanNguoiDung.Models12._1234
{
    public class LicenseExpiryWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LicenseExpiryWorker> _logger;

        public LicenseExpiryWorker(IServiceProvider serviceProvider, ILogger<LicenseExpiryWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<TmdtContext>();
                    // GỌI SERVICE Ở ĐÂY để không phải viết lại code gửi mail
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                    var today = DateOnly.FromDateTime(DateTime.Now);
                    var alertDate = today.AddDays(30);

                    var listNotify = await dbContext.TaiXes
                        .Include(t => t.MaNguoiDungNavigation)
                        .Where(t => t.NgayHetHanBang == alertDate)
                        .ToListAsync();

                    foreach (var tx in listNotify)
                    {
                        var emailNguoiDung = tx.MaNguoiDungNavigation?.Email;
                        var tenNguoiDung = tx.MaNguoiDungNavigation?.HoTenNhanVien ?? "Tài xế";

                        if (!string.IsNullOrEmpty(emailNguoiDung))
                        {
                            try
                            {
                                // Sử dụng service đã được định nghĩa
                                await emailService.SendEmailAsync(emailNguoiDung, tenNguoiDung, tx.NgayHetHanBang.ToString("dd/MM/yyyy"));
                                _logger.LogInformation($"Worker: Gửi mail thành công cho {emailNguoiDung}");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"Worker: Lỗi gửi mail cho {emailNguoiDung}: {ex.Message}");
                            }
                        }
                    }
                }
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
    }
}
