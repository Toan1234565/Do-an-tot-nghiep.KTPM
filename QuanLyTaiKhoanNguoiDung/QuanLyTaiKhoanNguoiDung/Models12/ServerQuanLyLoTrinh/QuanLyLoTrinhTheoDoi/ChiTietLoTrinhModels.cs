using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyTaiSan.QuanLyPhuongTien;

namespace QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyLoTrinh.QuanLyLoTrinhTheoDoi
{
    public class ChiTietLoTrinhModels
    {
        // 1. Thông tin cơ bản của Lộ trình
        public int MaLoTrinh { get; set; }
        public DateTime? ThoiGianBatDauKeHoach { get; set; }
        public DateTime? ThoiGianBatDauThucTe { get; set; }
        public string? TrangThai { get; set; }
        public int? MaTaiXeThucHienChinh { get; set; }
        public int? MaTaiXeThucHienPhu { get; set; }
        // 2. Thông tin nhân sự và phương tiện (Đã được map từ liên server)
        public string? TenTaiXeThucHienChinh { get; set; }
        public string? TenTaiXeThucHienPhu { get; set; }
        public int? MaPhuongTien { get; set; }
        // 4. Thống kê nhanh
        public int? TongSoDonHang { get; set; }
        public int? TongSoDiemDung { get; set; }
        public double? khoiLuong { get; set; }

        // 3. Các danh sách con
        // Danh sách kiện hàng kèm thông tin chi tiết từ Server Đơn hàng
        public List<ChiTietLoTrinhKienHangModels> ChiTietLoTrinhKienHangs { get; set; } = new List<ChiTietLoTrinhKienHangModels>();

        // Danh sách điểm dừng kèm thông tin tọa độ từ Server Địa chỉ
        public List<DiemDungModels> DiemDungs { get; set; } = new List<DiemDungModels>();

        // Danh sách chi phí phát sinh
        public List<ChiPhiLoTrinhModels> ChiPhiLoTrinhs { get; set; } = new List<ChiPhiLoTrinhModels>();
        // Trong file ChiTietLoTrinhModels.cs
        public PhuongTienDetailModel? ThongTinPhuongTien { get; set; }
    }
    // Model phụ để chứa thông tin kiện hàng trong lộ trình
    public class ChiTietLoTrinhKienHangModels
    {
        public int? MaDonHang { get; set; }
        public string? TrangThaiTrenXe { get; set; }
        public ChiTietDonHangLoTrinhModel? ThongTinDonHang { get; set; }
    }
    public class ChiTietDonHangLoTrinhModel
    {
        public string? TenNguoiNhan { get; set; }

        public string? SdtNguoiNhan { get; set; }

        public int? MaDiaChiNhanHang { get; set; }

        public int MaDiaChiLayHang { get; set; }

        public int MaKhachHang { get; set; }
    }
}
