using Microsoft.EntityFrameworkCore;
using QuanLyLoTrinhTheoDoi.Models;
using System.Threading.Tasks;

namespace QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer
{
    public interface IPhuongTienTaiXeService
    {
        /// <summary>
        /// Lấy thông tin mapping giữa Xe và Tài xế, có hỗ trợ fallback về Ca Chuyến Dài (MaCa = 8) nếu ca tiêu chuẩn không cấu hình.
        /// </summary>
        Task<PhuongTienTaiXe?> GetMappingByVehicleAsync(int maPhuongTien, int maCa);
    }

    public class PhuongTienTaiXeService : IPhuongTienTaiXeService
    {
        private readonly TmdtContext _context;
        public PhuongTienTaiXeService(TmdtContext context) => _context = context;

        public async Task<PhuongTienTaiXe?> GetMappingByVehicleAsync(int maPhuongTien, int maCa)
        {
            // 1. Ưu tiên tìm đúng cặp Xe - Tài xế đang Active trong ca tiêu chuẩn truyền vào (Ví dụ: Ca 1, Ca 2, Ca 3)
            var mapping = await _context.PhuongTienTaiXes
                .FirstOrDefaultAsync(x => x.MaPhuongTien == maPhuongTien
                                     && x.MaCa == maCa
                                     && x.IsActive == true);

            // 2. CƠ CHẾ DỰ PHÒNG (FALLBACK): 
            // Nếu ca tiêu chuẩn không có và ca truyền vào hiện tại không phải là ca chuyến dài (khác 8)
            // Hệ thống tự động tìm cấu hình "Ca Chuyến dài (Linh hoạt 24h)" - MaCa = 8 của xe này.
            if (mapping == null && maCa != 8)
            {
                mapping = await _context.PhuongTienTaiXes
                    .FirstOrDefaultAsync(x => x.MaPhuongTien == maPhuongTien
                                         && x.MaCa == 8
                                         && x.IsActive == true);
            }

            return mapping;
        }
    }
}