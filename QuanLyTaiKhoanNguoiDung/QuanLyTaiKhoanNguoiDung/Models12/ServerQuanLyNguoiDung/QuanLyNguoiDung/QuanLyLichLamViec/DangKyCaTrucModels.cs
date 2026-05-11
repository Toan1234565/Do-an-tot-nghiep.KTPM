using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyNguoiDung.QuanLyNhanVien;

namespace QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyNguoiDung.QuanLyLichLamViec
{
    public class DangKyCaTrucModels
    {
        public int MaDangKy { get; set; }

        public int MaNguoiDung { get; set; }

        public string? HoTenNhanVien { get; set; }

        public int MaCa { get; set; }

        public DateOnly NgayTruc { get; set; }

        public string? TrangThai { get; set; }

        public virtual CaLamViecModels? MaCaNavigation { get; set; }

        public virtual NguoiDungDetailModel MaNguoiDungNavigation { get; set; } = null!;
    }
}
