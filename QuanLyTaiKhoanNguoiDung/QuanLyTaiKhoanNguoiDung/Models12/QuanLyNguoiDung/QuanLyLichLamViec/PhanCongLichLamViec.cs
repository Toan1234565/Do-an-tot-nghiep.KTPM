namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyLichLamViec
{
    public class PhanCongLichLamViec
    {
        public int MaNguoiDung { get; set; }
        public int MaCa { get; set; }
        public DateOnly NgayTruc { get; set; }
        public string TrangThai { get; set; } = "Chờ duyệt"; // Hoặc "Đã duyệt"
    }
}
