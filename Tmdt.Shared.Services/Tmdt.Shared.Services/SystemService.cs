using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Tmdt.Shared.Services;

namespace Tmdt.Shared.Services
{
    public class SystemService : ISystemService
    {
        private readonly RabbitMQClient _rabbitMQ;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly CacheSignalService _cacheSignal;

        public static CancellationTokenSource ResetCacheSignal = new CancellationTokenSource();

        public SystemService(RabbitMQClient rabbitMQ, IHttpContextAccessor httpContextAccessor, CacheSignalService cacheSignal)
        {
            _rabbitMQ = rabbitMQ;
            _httpContextAccessor = httpContextAccessor;
            _cacheSignal = cacheSignal;
        }

        public int? GetCurrentUserId()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return null;

            try
            {
                // 1. ƯU TIÊN: Lấy từ Claims (Vì isAuth đã true)
                if (context.User?.Identity?.IsAuthenticated == true)
                {
                    // Kiểm tra NameIdentifier (chuẩn ASP.NET) hoặc claim "MaNguoiDung" tự định nghĩa
                    var claim = context.User.FindFirst(ClaimTypes.NameIdentifier)
                                ?? context.User.FindFirst("MaNguoiDung");

                    if (claim != null && int.TryParse(claim.Value, out int userId))
                    {
                        return userId;
                    }
                }

                // 2. DỰ PHÒNG: Lấy từ Session Redis
                var sessionUserId = context.Session?.GetString("MaNguoiDung");
                if (!string.IsNullOrEmpty(sessionUserId) && int.TryParse(sessionUserId, out int sUserId))
                {
                    return sUserId;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SystemService] Lỗi lấy UserId: {ex.Message}");
            }

            return null;
        }

        public async Task GhiLogVaResetCacheAsync(string dichVu, string thaoTac, string bang, string maDoiTuong, object dataCu, object dataMoi)
        {
            var context = _httpContextAccessor.HttpContext;
            var userId = GetCurrentUserId();

            // --- TỐI ƯU LẤY HỌ TÊN NGƯỜI THỰC HIỆN ---
            string hoTen = "Hệ thống";

            if (context != null)
            {
                // Ưu tiên 1: Lấy trực tiếp từ Identity Name
                var identityName = context.User?.Identity?.Name;

                // Ưu tiên 2: Lấy từ Claim Name thủ công (Nếu Identity.Name bị null)
                var nameClaim = context.User?.FindFirst(ClaimTypes.Name)?.Value;

                // Ưu tiên 3: Lấy từ Session (Phao cứu sinh cuối cùng)
                var sessionName = context.Session?.GetString("HoTenNhanVien");

                if (!string.IsNullOrEmpty(identityName))
                    hoTen = identityName;
                else if (!string.IsNullOrEmpty(nameClaim))
                    hoTen = nameClaim;
                else if (!string.IsNullOrEmpty(sessionName))
                    hoTen = sessionName;
                else if (userId.HasValue)
                    hoTen = $"User ID: {userId}";
            }

            var log = new LogMessage
            {
                TenDichVu = dichVu,
                LoaiThaoTac = thaoTac,
                TenBangLienQuan = bang,
                MaDoiTuong = maDoiTuong,
                DuLieuCu = dataCu,
                DuLieuMoi = dataMoi,
                MaNguoiDung = userId,
                NguoiThucHien = hoTen,
                // Lấy IP, ưu tiên X-Forwarded-For nếu chạy sau Proxy/Load Balancer
                DiaChiIp = context?.Request.Headers["X-Forwarded-For"].ToString()
                           ?? context?.Connection?.RemoteIpAddress?.ToString()
                           ?? "127.0.0.1",
                TrangThaiThaoTac = true,
                ThoiGianThucHien = DateTime.Now
            };

            // 1. Gửi Log qua RabbitMQ
            await _rabbitMQ.SendLogAsync(log);

            // 2. Reset Cache Signal (Đồng bộ hệ thống)
            try
            {
                if (!ResetCacheSignal.IsCancellationRequested)
                {
                    ResetCacheSignal.Cancel();
                }
            }
            catch (ObjectDisposedException) { }

            ResetCacheSignal = new CancellationTokenSource();
            _cacheSignal.Reset();
        }
    }
}