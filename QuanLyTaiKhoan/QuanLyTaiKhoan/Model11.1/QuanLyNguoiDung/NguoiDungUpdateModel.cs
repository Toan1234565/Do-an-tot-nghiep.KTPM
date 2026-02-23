using System.ComponentModel.DataAnnotations;

namespace QuanLyTaiKhoan.Model11._1.QuanLyNguoiDung
{
    public class NguoiDungUpdateModel
    {
        public int MaNguoiDung { get; set; }

        [Required(ErrorMessage = "Họ tên nhân viên là bắt buộc")]
        public string? HoTenNhanVien { get; set; }

        [Required(ErrorMessage = "Địa chỉ là bắt buộc")]
        public string? DiaChi { get; set; }

        [Required(ErrorMessage = "Email là bắt buộc")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Số điện thoại là bắt buộc")]
        public string? SoDienThoai { get; set; }

        public int? MaChucVu { get; set; }

        public DateOnly? NgaySinh { get; set; }

        public string? GioiTinh { get; set; }

        [Required(ErrorMessage = "Số CCCD  là bắt buộc")]
        public string? SoCccd { get; set; }

        public string? NoiSinh { get; set; }

        public string? SoTaiKhoan { get; set; }

        public string? TenNganHang { get; set; }

        public string? BaoHiemXaHoi { get; set; }
    }
}
