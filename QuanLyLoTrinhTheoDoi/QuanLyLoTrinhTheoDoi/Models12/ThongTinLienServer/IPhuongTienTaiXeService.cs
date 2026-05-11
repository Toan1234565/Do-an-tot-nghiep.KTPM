using Microsoft.EntityFrameworkCore;
using QuanLyLoTrinhTheoDoi.Models;

namespace QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer
{
    public interface IPhuongTienTaiXeService
    {
        // Hàm này dùng để gọi nội bộ trong code điều phối tự động
        Task<PhuongTienTaiXe?> GetMappingByVehicleAsync(int maPhuongTien, int maCa);
    }

    public class PhuongTienTaiXeService : IPhuongTienTaiXeService
    {
        private readonly TmdtContext _context;
        public PhuongTienTaiXeService(TmdtContext context) => _context = context;

        public async Task<PhuongTienTaiXe?> GetMappingByVehicleAsync(int maPhuongTien, int maCa)
        {
            return await _context.PhuongTienTaiXes
                .FirstOrDefaultAsync(x => x.MaPhuongTien == maPhuongTien
                                     && x.MaCa == maCa
                                     && x.IsActive == true);
        }
    }
}
