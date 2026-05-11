namespace QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyLoTrinh.QuanLyLoTrinhTheoDoi
{
    public class LoTrinhModels
    {
        public int MaLoTrinh { get; set; }

        public DateTime? ThoiGianBatDauKeHoach { get; set; }

        public DateTime? ThoiGianBatDauThucTe { get; set; }

        public string? TrangThai { get; set; }

        public int? TongSoDonHang { get; set; }
        public int? TongSoDiemDung { get; set; }

        public int ? MaNguoiDung { get; set; }

        public int? MaPhuongTien { get; set; }

        public string? TenTaiXeThucHien { get; internal set; }
        public string? BienSoXe { get; internal set; }

    }
}
