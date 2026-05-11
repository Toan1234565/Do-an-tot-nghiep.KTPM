using Microsoft.Identity.Client;

namespace QuanLyKho.Models1.QuanLyPhuongTien
{
    public class BaoTriModel
    {
        public int MaPhuongTien { get; set; }
        public string? BienSo { get; set; }
        public string? TenLoaiXe { get; set; }
        public double? SoKmHienTai { get; set; }
        public List<string>? CacHangMucToiHan { get; set; }
        public string? GhiChu { get; set; }
        public string? TrangThai { get; set; }
    }
}
