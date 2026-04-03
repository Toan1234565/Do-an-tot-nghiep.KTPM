namespace QuanLyDonHang.Models1.QuanLyDieuPhoiGomHang
{
    public class RoutingOrderMessage
    {
        public int MaDonHang { get; set; }
        public int? MaKhoVao { get; set; }
        public int? MaDiaChiNhanHang { get; set; }
        public string? MaVungH3Nhan { get; set; }
        public string? MaVungH3Giao { get; set; }
        public double TongKhoiLuong { get; set; }
        public double TongTheTich { get; set; }
        public string? TrangThaiMoi { get; set; }
        public DateTime ThoiGian { get; set; }
    }
}
