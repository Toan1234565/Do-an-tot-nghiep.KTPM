namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyNhatKyHeThong
{
    public class SystemService : ISystemService
    {
        private readonly TmdtContext _context;
        private readonly RabbitMQClient _rabbitMQ;
        private readonly IHttpContextAccessor _httpContextAccessor;

        // Dùng chung một Signal tĩnh cho toàn bộ ứng dụng
        public static CancellationTokenSource ResetCacheSignal = new CancellationTokenSource();
        private readonly CacheSignalService _cacheSignal;
        public SystemService(TmdtContext context, RabbitMQClient rabbitMQ, IHttpContextAccessor httpContextAccessor, CacheSignalService cacheSignal)
        {
            _context = context;
            _rabbitMQ = rabbitMQ;
            _httpContextAccessor = httpContextAccessor;
            _cacheSignal = cacheSignal;
        }

        public int? GetCurrentUserId()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var claim = user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            return claim != null ? int.Parse(claim.Value) : null;
        }

        public async Task GhiLogVaResetCacheAsync(string dichVu, string thaoTac, string bang, string maDoiTuong, object dataCu, object dataMoi)
        {
            var userId = GetCurrentUserId();
            var user = await _context.NguoiDungs.FindAsync(userId);

            var log = new LogMessage
            {
                TenDichVu = dichVu,
                LoaiThaoTac = thaoTac,
                TenBangLienQuan = bang,
                MaDoiTuong = maDoiTuong,
                DuLieuCu = dataCu,
                DuLieuMoi = dataMoi,
                NguoiThucHien = user?.HoTenNhanVien ?? "Hệ thống",
                DiaChiIp = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                TrangThaiThaoTac = true,
                ThoiGianThucHien = DateTime.Now
            };

            // 1. Bắn Log
            await _rabbitMQ.SendLogAsync(log);

            // 2. Reset Cache toàn hệ thống
            ResetCacheSignal.Cancel();
            ResetCacheSignal = new CancellationTokenSource();
            _cacheSignal.Reset();
        }
    }
}
