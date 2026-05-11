namespace QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyDonHang.QuanLyDonHang
{
    public class ApiResponse
    {
        public int TotalItems { get; set; }
        public List<DonHangModels>? Data { get; set; }
    }
}
