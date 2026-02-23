namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyKhoBai
{
    public class QuanLyKhobaiModels
    {
        public int MaKho { get; set; }

        public int MaDiaChi { get; set; }

        public int? MaQuanLy { get; set; }

        public double? DungTichM3 { get; set; }

        public string? TenKhoBai { get; set; }

        public decimal? DienTichM2 { get; set; }

        public string? TrangThai { get; set; }

        public decimal? SucChua { get; set; }

        public string? SoDienThoaiKho { get; set; }

        public int? MaLoaiKho { get; set; }

        public string? TenLoaiKho { get; set; }
        public virtual LoaiKhoModel? MaLoaiKhoNavigation { get; set; }

    }
}
