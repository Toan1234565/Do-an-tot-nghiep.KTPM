namespace QuanLyLoTrinhTheoDoi.Models12.DieuPhoiLoTrinh
{
    public class DonHangDetailDto
    {
        public int MaDonHang { get; set; }
        public string? TenNguoiNhan { get; set; }
        public string? SoDienThoaiNhan { get; set; }

        // Cực kỳ quan trọng để tính toán lộ trình tiếp theo
        public int MaDiaChiGiao { get; set; }
        public string? MaVungH3 { get; set; }

        // Các thông tin bổ sung nếu cần hiển thị
        public decimal? TongTrongLuong { get; set; }
        public string? TrangThai { get; set; }

        public int? MaDiaChiNhanHang { get; internal set; }
        public string? MaVungH3Giao { get; internal set; }
    }
}