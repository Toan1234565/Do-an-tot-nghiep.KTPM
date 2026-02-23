namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyDonHang
{
    public class ThongKeDonHangModels
    {
        // Mã loại dịch vụ (tương ứng với MaLoaiDv trong bảng Don_Hang)
        public int? MaLoaiDv { get; set; }

        // Tên dịch vụ (Nếu bạn đã map tên ở API hoặc sẽ map ở View)
        public string? TenDichVu { get; set; }

        // Số lượng đơn hàng sử dụng dịch vụ này
        public int SoLuongDonHang { get; set; }

        // --- Các thuộc tính mở rộng (Nếu cần báo cáo chuyên sâu hơn) ---

        // Tỷ lệ phần trăm (ví dụ: 25%)
        public double PhanTram { get; set; }

        // Tổng số tiền hoặc khối lượng nếu API có tính toán thêm
        // public decimal? TongDoanhThu { get; set; }
    }
}
