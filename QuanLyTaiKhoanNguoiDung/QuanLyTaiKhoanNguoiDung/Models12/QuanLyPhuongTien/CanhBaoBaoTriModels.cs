namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyPhuongTien
{
    public class CanhBaoBaoTriModels
    {
        public int? MaDinhMuc { get; set; }
        public string TenHangMuc { get; set; } = string.Empty;
        public string LyDo { get; set; } = string.Empty; // "Hết KM" hoặc "Quá hạn ngày"
        public string TrangThai { get; set; } = string.Empty; // "Quá hạn", "Sắp đến hạn"
        public double? ConLaiKm { get; set; }
        public int? ConLaiNgay { get; set; }
        public DateOnly? NgayDuKien { get; set; }
    }
}
