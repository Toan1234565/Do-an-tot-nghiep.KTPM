namespace QuanLyDonHang.Models1.ServerKhachHang
{
    public class UpdateMultiWarehouseRequest
    {
        public List<int> DanhSachMaDonHang { get; set; } = new List<int>();
        public int MaKhoMoi { get; set; }
        public string? TrangThaiMoi { get; set; } // Ví dụ: "Chờ trung chuyển", "Đã nhập kho"
    }
}
