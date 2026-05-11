namespace QuanLyKhachHang.Models1.LienServer
{
    public class KienHangModels
    {
        public string MaVach { get; set; }
        public double? KhoiLuong { get; set; } // Dùng double hoặc decimal tùy DB
        public decimal? SoTien { get; set; }
        public string TenLoaiHang { get; set; }
    }
}