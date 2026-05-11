using QuanLyLoTrinhTheoDoi.Models12.QuanLyLoTrinh.cs;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer;

namespace QuanLyLoTrinhTheoDoi.Models12
{
    public class ChiTietLoTrinhModels
    {
        // 1. Thông tin cơ bản của Lộ trình
        public int MaLoTrinh { get; set; }
        public DateTime? ThoiGianBatDauKeHoach { get; set; }
        public DateTime? ThoiGianBatDauThucTe { get; set; }
        public string? TrangThai { get; set; }

        // 2. Thông tin nhân sự và phương tiện (Đã được map từ liên server)
        public string? TenTaiXeThucHienChinh { get; set; }
        public string? TenTaiXeThucHienPhu { get; set; }
        public int? MaPhuongTien { get; set; }

        // 3. Các danh sách con
        // Danh sách kiện hàng kèm thông tin chi tiết từ Server Đơn hàng
        public List<ChiTietLoTrinhKienHangModels> ChiTietLoTrinhKienHangs { get; set; } = new List<ChiTietLoTrinhKienHangModels>();

        // Danh sách điểm dừng kèm thông tin tọa độ từ Server Địa chỉ
        public List<DiemDungModels> DiemDungs { get; set; } = new List<DiemDungModels>();

        // Danh sách chi phí phát sinh
        public List<ChiPhiLoTrinhModels> ChiPhiLoTrinhs { get; set; } = new List<ChiPhiLoTrinhModels>();
        // Trong file ChiTietLoTrinhModels.cs
        public PhuongTienDetailModel? ThongTinPhuongTien { get; set; }
        // 4. Thống kê nhanh
        public int TongSoDonHang { get; set; }
        public int TongSoDiemDung { get; set; }
        
    }

    // Model phụ để chứa thông tin kiện hàng trong lộ trình
    public class ChiTietLoTrinhKienHangModels
    {
        public int? MaDonHang { get; set; }
        public string? TrangThaiTrenXe { get; set; }
        public ChiTietDonHangLoTrinhModel? ThongTinDonHang { get; set; }
    }
}