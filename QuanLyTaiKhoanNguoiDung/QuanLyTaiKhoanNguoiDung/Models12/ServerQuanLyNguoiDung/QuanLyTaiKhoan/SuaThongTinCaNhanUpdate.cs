namespace QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyTaiKhoan
{
    public class SuaThongTinCaNhanUpdate
    {
        public string? HoTenNhanVien { get; set; }


        public int? MaDiaChi { get; set; }


        public string? Email { get; set; }


        public string? SoDienThoai { get; set; }

        public DateOnly? NgaySinh { get; set; }

        public string? GioiTinh { get; set; }

        public string? SoCccd { get; set; }

        public string? NoiSinh { get; set; }

        public string? SoTaiKhoan { get; set; }
        public string? TenNganHang { get; internal set; }
    }
}
