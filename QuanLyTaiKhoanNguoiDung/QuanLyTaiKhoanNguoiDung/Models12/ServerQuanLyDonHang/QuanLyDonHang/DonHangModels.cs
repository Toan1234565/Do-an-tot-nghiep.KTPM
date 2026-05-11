using QuanLyTaiKhoanNguoiDung.Models12.SeverQuanLyKhachHang.QuanLyDiaChi;
using QuanLyTaiKhoanNguoiDung.Models12.SeverQuanLyKhachHang.QuanLyKhachHang;

namespace QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyDonHang.QuanLyDonHang
{
    public class DonHangModels
    {
        public int MaDonHang { get; set; }

        public int MaKhachHang { get; set; }

        public DateTime ThoiGianTao { get; set; }

        public int MaDiaChiLayHang { get; set; }

        public string? TrangThaiHienTai { get; set; }

        public string? TenDonHang { get; set; }

        public int? MaHopDongNgoai { get; set; }

        public string? GhiChuDacBiet { get; set; }

        public int? MaDiaChiNhanHang { get; set; }

        public string? TenNguoiNhan { get; set; }

        public string? SdtNguoiNhan { get; set; }

        public int? MaMucDoDv { get; set; }

        public decimal? TongTienDuKien { get; set; }

        public decimal? TongTienThucTe { get; set; }

        public DateTime? ThoiGianGiaoDuKien { get; set; }

        public int? MaKhuyenMai { get; set; }

        public string? MaVungH3Nhan { get; set; }

        public string? MaVungH3Giao { get; set; }

        public string? TrangThaiThanhToanTong { get; set; }

        public int? MaPttt { get; set; }

        public int? MaKhoHienTai { get; set; }

        public int? MaDichVu { get; set; }

        public KhachHangModels ThongTinKhachHang { get; set; }
        public DiaChiModel DiaChiLayHang { get; set; }
        public DiaChiModel DiaChiNhanHang { get; set; }
        public DiaChiModel DiaChiKhoHienTai { get; set; }

        public virtual ICollection<KienHangModels> KienHangs { get; set; } = new List<KienHangModels>();
    }
}
