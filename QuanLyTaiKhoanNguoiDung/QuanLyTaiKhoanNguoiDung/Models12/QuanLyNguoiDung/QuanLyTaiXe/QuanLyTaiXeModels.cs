namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyTaiXe
{
    public class QuanLyTaiXeModels
    {
        public int MaNguoiDung { get; set; }
        // Trường lấy từ NguoiDung
        public string? HoTenTaiXe { get; set; }

        // Các trường lấy từ TaiXe
        public string SoBangLai { get; set; } = null!;
        public string LoaiBangLai { get; set; } = null!;
        public int? KinhNghiemNam { get; set; }
        public string? TrangThaiHoatDong { get; set; }
        public decimal? DiemUyTin { get; set; }
    }
}