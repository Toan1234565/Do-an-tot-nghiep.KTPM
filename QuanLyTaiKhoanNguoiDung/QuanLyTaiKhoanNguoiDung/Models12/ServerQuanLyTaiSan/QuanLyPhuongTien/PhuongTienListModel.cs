namespace QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyTaiSan.QuanLyPhuongTien
{
    public class PhuongTienListModel
    {
        public int MaPhuongTien { get; set; }

        public string? BienSo { get; set; }

        public int MaLoaiXe { get; set; }

        public string? TenLoaiXe { get; set; }

        public double? TaiTrongToiDaKg { get; set; }

        public double? TheTichToiDaM3 { get; set; }

        public double? MucTieuHaoNhienLieu { get; set; }

        public string? TrangThai { get; set; }

        public string? TenKho { get; set; }

        public string? TenNguoiPhuTrach { get; set; }

        public int? MaNguoiDung { get; set; }
    }
}
