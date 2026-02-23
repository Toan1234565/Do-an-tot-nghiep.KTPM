namespace QuanLyKho.Models1.QuanLyXe
{
    public class CanhBaoBaoTriModel
    {
        public string TenHangMuc { get; set; } = string.Empty;
        public string LyDo { get; set; } = string.Empty; // "Hết KM" hoặc "Quá hạn ngày"
        public string TrangThai { get; set; } = string.Empty; // "Quá hạn", "Sắp đến hạn"
        public DateOnly? NgayDuKien { get; set; }
        public double? ConLaiKm { get; set; }
        public int? ConLaiNgay { get; set; }
    }

   
}
