namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyDonHang
{
    public class KienHangModels
    {
        public int MaKienHang { get; set; }

        public int MaDonHang { get; set; }

        public string? MaVach { get; set; }

        public int MaDiaChiLay { get; set; }

        public double? KhoiLuong { get; set; }

        public double? TheTich { get; set; }

        public bool DaThuGom { get; set; }

        public decimal? SoTien { get; set; }

        public bool DaThanhToan { get; set; }

        public int? MaKhoHienTai { get; set; }

        public virtual DonHangModels MaDonHangNavigation { get; set; } = null!;

        public virtual DanhMucLoaiHangModels? MaLoaiHangNavigation { get; set; }
    }
}
