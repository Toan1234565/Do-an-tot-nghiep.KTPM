using System.ComponentModel.DataAnnotations;

namespace TaiKhoan1._1.Model11._1.QuanLyTaiKhoan
{
    public class DangNhapModel
    {
        [Required(ErrorMessage = "Tên đăng nhập là bắt buộc")]
        public string? TenDangNhap { get; set; }

        [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
        public string? MatKhau { get; set; }
    }
}
