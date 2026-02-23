using QuanLyTaiKhoanNguoiDung.Models12.QuanLyKhachHang;

namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyKhuyenMai
{
    public class LichSuDungMaModels
    {
        public int MaLichSu { get; set; }

        public int MaKhachHang { get; set; }

        public int MaKhuyenMai { get; set; }

        public int MaDonHang { get; set; }

        public DateTime? NgaySuDung { get; set; }

        public virtual KhachHangModels? MaKhachHangNavigation { get; set; }

        public virtual KhuyenMaiModels? MaKhuyenMaiNavigation { get; set; }
    }
}
