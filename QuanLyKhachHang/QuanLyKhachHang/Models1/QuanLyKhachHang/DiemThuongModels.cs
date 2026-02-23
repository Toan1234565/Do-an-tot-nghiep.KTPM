using QuanLyKhachHang.Models;

namespace QuanLyKhachHang.Models1.QuanLyKhachHang
{
    public class DiemThuongModels
    {
        public int MaDiem { get; set; }

        public int MaKhachHang { get; set; }

        public int? TongDiemTichLuy { get; set; }

        public int? DiemDaDung { get; set; }

        public DateTime? NgayCapNhatCuoi { get; set; }

        public virtual KhachHangModels MaKhachHangNavigation { get; set; } = null!;
    }
}
