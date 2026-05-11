using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyDonHang.QuanLyDonHang;

namespace QuanLyTaiKhoanNguoiDung.Models12.SeverQuanLyKhachHang.QuanLyKhachHang
{
    public class PagedDonHangResponse
    {
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
        public List<DonHangModels> Data { get; set; }
    }
}
