using QuanLyTaiKhoanNguoiDung.Models;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyNhanVien;
using System.ComponentModel.DataAnnotations;

namespace QuanLyTaiKhoanNguoiDung.QuanLyTaiKhoan
{
    public class TaiKhoanCreate
    {

        public string? MaNguoiDung { get; set; }

        [Required(ErrorMessage = "Tên đăng nhập là bắt buộc")]
        [StringLength(50, MinimumLength = 5, ErrorMessage = "Tên đăng nhập phải từ 5 đến 50 ký tự")]
        public string? TenDangNhap { get; set; }

        [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6 ký tự trở lên")]
        public string? MatKhauHash { get; set; } // Trong thực tế, tên nên là MatKhau (Password) và việc Hash nên được thực hiện trong API/Service


        // Mặc định là True khi tạo, có thể ẩn trong form
        public bool HoatDong { get; set; } = true;
        public virtual NguoiDungModel? NguoiDung { get; set; }
    }
}
