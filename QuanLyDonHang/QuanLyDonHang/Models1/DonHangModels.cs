using QuanLyDonHang.Models;
using QuanLyDonHang.Models1.ServerKhachHang;

namespace QuanLyDonHang.Models1
{
    public class DonHangModels
    {
        public int MaDonHang { get; set; }

        public int MaKhachHang { get; set; }

        public DateTime ThoiGianTao { get; set; }

        public int MaDiaChiNhanHang { get; set; }

        public string? TrangThaiHienTai { get; set; }

        public string? TenDonHang { get; set; }

        public int? MaLoaiDv { get; set; }

        public int? MaHopDongNgoai { get; set; }

        public bool? LaDonGiaoThang { get; set; }

        public string? GhiChuDacBiet { get; set; }

       
        public int? MaDiaChiLayHang { get; set; }

        public string? TenNguoiNhan { get; set; }

        public string? SdtNguoiNhan { get; set; }

        public int? SoLuongKienHang { get; set; }

        public int? MaMucDoDv { get; set; }

        public decimal? TongTienDuKien { get; set; }

        public decimal? TongTienThucTe { get; set; }

        public string? MaVungH3Nhan { get; set; }

        public string? MaVungH3Giao { get; set; }

        public int? MaKhoHienTai { get; set; }

        public string? TrangThaiThanhToanTong { get; set; }

        public string? TenPhuongThucTT { get; set; }

        // Thông tin từ Server Khách Hàng
        public KhachHangModels ThongTinKhachHang { get; set; }

        // Thông tin từ Server Địa Chỉ (Để hiện lộ trình)
        public DiaChiModel DiaChiLayHang { get; set; }
        public DiaChiModel DiaChiNhanHang { get; set; }
        public DiaChiModel DiaChiKhoHienTai { get; set; } // Vị trí hiện tại trên timeline

        public virtual ICollection<KienHangModels> KienHangs { get; set; } = new List<KienHangModels>();
    }
}
