namespace QuanLyKhachHang.Models1.CauHinhTichDiem
{
    // Model nhận dữ liệu
    public class TichDiemRequest
    {
        public int MaKhachHang { get; set; }
        public int MaDonHang { get; set; }
        public decimal SoTienThanhToan { get; set; } // Số tiền sau khi đã trừ KM/Điểm cũ
    }
}
