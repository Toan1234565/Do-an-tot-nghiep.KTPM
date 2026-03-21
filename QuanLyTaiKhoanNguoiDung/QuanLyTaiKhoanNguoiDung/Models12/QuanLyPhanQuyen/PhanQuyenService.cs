using Microsoft.EntityFrameworkCore;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyTaiKhoan;

namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyPhanQuyen
{
    public class PhanQuyenService
    {
        private readonly TmdtContext _context;

        public PhanQuyenService(TmdtContext context)
        {
            _context = context;
        }

        public async Task<UserPermission?> GetUserPermissionAsync(int? userId)
        {
            if (userId == null) return null;

            var user = await _context.NguoiDungs
                .Include(nd => nd.MaChucVuNavigation)
                .ThenInclude(cv => cv.MaVaiTroNavigation)
                .AsNoTracking()
                .FirstOrDefaultAsync(nd => nd.MaNguoiDung == userId);

            if (user == null) return null;

            string tenVaiTro = user.MaChucVuNavigation?.TenChucVu ?? "";

            return new UserPermission
            {
                UserId = user.MaNguoiDung,
                HoTen = user.HoTenNhanVien ?? "N/A",
                MaKho = user.MaKho,
                TenVaiTro = tenVaiTro,
                // Logic check role
                IsQuanLyTong = tenVaiTro.Contains("Quản lý tổng") || tenVaiTro.Contains("Admin"),
                IsQuanLyKho = tenVaiTro.Contains("Quản lý chi nhánh") || tenVaiTro.Contains("Quản lý kho")
            };
        }
    }
}
