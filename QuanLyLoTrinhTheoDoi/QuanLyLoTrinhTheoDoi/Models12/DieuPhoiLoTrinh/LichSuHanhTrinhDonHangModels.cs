namespace QuanLyLoTrinhTheoDoi.Models12.DieuPhoiLoTrinh
{
    public class LichSuHanhTrinhDonHangModels
    {
        public int MaLichSu { get; set; }

        public int MaDonHang { get; set; }

        public int? MaLoTrinh { get; set; }

        public int? MaKho { get; set; }

        public string TrangThai { get; set; } = null!;

        public string? ViTriHienTai { get; set; }

        public DateTime? ThoiGianCapNhat { get; set; }

        public string? GhiChu { get; set; }
    }
}
