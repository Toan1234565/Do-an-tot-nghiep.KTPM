namespace QuanLyKhachHang.Models1.QuanLyDiaChi
{
    // DTO để nhận danh sách ID từ Service Kho gửi sang
    public class DanhSachMaDiaChiDto
    {
        public List<int>? MaDiaChis { get; set; }
    }

    // DTO để trả về kết quả tọa độ
    public class ToaDoResponseDto
    {
        public int MaDiaChi { get; set; }
        public double? ViDo { get; set; }
        public double? KinhDo { get; set; }
    }
}
