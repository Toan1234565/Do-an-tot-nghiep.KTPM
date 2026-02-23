namespace QuanLyKhachHang.Models1.CauHinhTichDiem
{
    public class DoiDiemRequest
    {
        public int MaKhachHang { get; set; }
        public int SoDiemMuonDung { get; set; }
        public decimal TongTienDonHang { get; set; } // Để kiểm tra hạn mức giảm
    }
}
