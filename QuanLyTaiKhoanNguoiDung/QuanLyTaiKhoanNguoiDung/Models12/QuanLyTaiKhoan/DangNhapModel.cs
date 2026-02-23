using System.ComponentModel.DataAnnotations;

namespace QuanLyTaiKhoanNguoiDung.QuanLyTaiKhoan
{
    public class DangNhapModel
    {
        [Required(ErrorMessage = "Tên đăng nhập là bắt buộc")]
        public string? TenDangNhap { get; set; }

        [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
        public string? MatKhau { get; set; }
    }
}
