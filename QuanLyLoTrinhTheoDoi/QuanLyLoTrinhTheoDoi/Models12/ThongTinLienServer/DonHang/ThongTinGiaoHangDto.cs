namespace QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.DonHang
{
    public class ThongTinGiaoHangDto
    {
        public string? MienGiaoHang { get; set; } // Trả về "North", "Central", hoặc "South"
        public int? MaDiaChiNhanHang { get; set; }
        public int? MaDiaChiLayHang { get; set; }
        public string? MaVungH3Giao { get; set; }
    }
}
