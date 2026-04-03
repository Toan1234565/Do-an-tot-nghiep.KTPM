namespace QuanLyLoTrinhTheoDoi.Models12.DieuPhoiLoTrinh
{
    public class RoutingOrderMessage
    {
        public int MaDonHang { get; set; }
        public int? MaKhoVao { get; set; } // Nên để int? để khớp với DB
        public int? MaDiaChiNhanHang { get; set; }
        public int MaDiaChiLayHang { get; set; }
        public string? MaVungH3Nhan { get; set; } // Thêm trường này
        public string? MaVungH3Giao { get; set; } // Thêm trường này
        public double TongKhoiLuong { get; set; }
        public double TongTheTich { get; set; }
        public string? TrangThaiMoi { get; set; }
        public DateTime ThoiGian { get; set; }
    }
}