using QuanLyKhachHang.Models1.QuanLyKhachHang;

namespace QuanLyKhachHang.Models1.QuanLyHopDong
{
    public class ChiTietHopDongModes
    {
        public int MaHopDong { get; set; }

        public string? TenHopDong { get; set; }

        public int MaKhachHang { get; set; }

        public DateTime? NgayKy { get; set; }

        public DateTime? NgayHetHan { get; set; }

        public string? LoaiHangHoa { get; set; }

        public string? TrangThai { get; set; }       
    }
}
