using System.ComponentModel.DataAnnotations;

namespace QuanLyKhachHang.Models1.QuanLyBangGiaVung
{
    public class YeuCauTinhPhi
    {
        public string? ThanhPhoLay { get; set; }
        public string? ThanhPhoGiao { get; set; }
        public double? KhoiLuongTong { get; set; }
        public double? TheTichTong { get; set; } // Đơn vị m3 hoặc cm3 tùy quy ước
        public double? SoKm { get; set; } // Dùng nếu chọn dịch vụ vận tải (LoaiTinhGia = 2)
        public int? MaLoaiHang {  get; set; }
        public int? MaBangGiaVung { get; set; }
    }
}
