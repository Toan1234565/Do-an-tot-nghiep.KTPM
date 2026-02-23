namespace QuanLyKho.Models1.QuanLyXe
{
    public class DangKiemModel
    {
        public int IdDangKiem { get; set; }

        public int MaPhuongTien { get; set; }

        public string SoSeriGiayPhep { get; set; } = null!;

        public string? SoTemKiemDinh { get; set; }

        public DateOnly NgayKiemDinh { get; set; }

        public DateOnly NgayHetHan { get; set; }

        public string? DonViKiemDinh { get; set; }

        public DateOnly? PhiDuongBoDenNgay { get; set; }

        public string? GhiChu { get; set; }

        public DateTime? NgayTao { get; set; }

        public string? HinhAnhDangKiem { get; set; }
    }
}
