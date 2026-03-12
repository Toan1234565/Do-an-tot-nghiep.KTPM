namespace QuanLyDonHang.Models1.QuanLyDieuPhoiGomHang
{
    public class OrderMessageDto
    {
        public int MaDonHang { get; set; }
        public int MaKho { get; set; } // Cần thêm MaKho để biết lấy xe ở đâu
        public double KhoiLuong { get; set; } // Cần khối lượng để lọc xe phù hợp
        public int MaDiaChiGiao { get; set; }
        public int MaDiaChiLayHang { get; set; }
        public DateTime ThoiGian { get; set; }
    }
}
