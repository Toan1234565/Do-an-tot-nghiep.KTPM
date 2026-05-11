using QuanLyKho.Models;

namespace QuanLyKho.Models1.QuanLyDinhMucBaoTri
{
    public class DinhMucInputModel
    {
        public int MaDinhMuc { get; set; } // Dùng cho Update
        public int MaLoaiXe { get; set; }
        public string? TenHangMuc { get; set; }
        public double? DinhMucKm { get; set; }
        public int? DinhMucThang { get; set; }
    }
}
