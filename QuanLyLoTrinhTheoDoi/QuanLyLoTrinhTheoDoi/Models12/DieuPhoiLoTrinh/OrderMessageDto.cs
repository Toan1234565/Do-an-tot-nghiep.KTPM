using System.Text.Json.Serialization;

namespace QuanLyLoTrinhTheoDoi.Models12.DieuPhoiLoTrinh
{
    // 1. Class hứng trọn gói JSON từ Server Đơn Hàng
    public class DonHangResponse
    {
        [JsonPropertyName("clusters")]
        public List<ClusterFromApi> Clusters { get; set; } = new();
    }

    // 2. Class chi tiết từng cụm mà Server Đơn Hàng trả về
    public class ClusterFromApi
    {
        [JsonPropertyName("maDiaChiCum")]
        public int MaDiaChiCum { get; set; }

        [JsonPropertyName("danhSachMaDonHang")]
        public List<int> DanhSachMaDonHang { get; set; } = new();

        [JsonPropertyName("tongKhoiLuong")]
        public double TongKhoiLuong { get; set; }

        [JsonPropertyName("soLuongDonHang")]
        public int SoLuongDonHang { get; set; }
        public string? MaVungH3 { get; internal set; }
        public double TongTheTich { get; internal set; }
        public int MaDiaChiLayHang { get; internal set; }
    }
   
    public class DonHangDto
    {
        public int MaDonHang { get; set; }
        
        
        
        public string? TrangThaiHienTai { get; set; }
        public int? MaMucDoDv { get; set; }
        public List<KienHangDto> KienHangs { get; set; } = new();

        // Thuộc tính tính toán nhanh tổng khối lượng cụm
        public double TongKhoiLuong => KienHangs?.Sum(kh => kh.KhoiLuong ?? 0) ?? 0;

        public int MaDiaChiGiao { get; set; }
        public int MaDiaChiLayHang { get; set; }
    }

    public class KienHangDto
    {
        public double? KhoiLuong { get; set; }
        public double? TheTich { get; set; }
    }
    public class KhoGanNhatResponse
    {
        public int MaKho { get; set; }
        public string? TenKho { get; set; }
        public string? Distance { get; set; }
    }
    // Dữ liệu nhận từ RabbitMQ
    public class OrderMessageDto
    {
        [JsonPropertyName("MaDonHang")]
        public int MaDonHang { get; set; }

        [JsonPropertyName("MaKhoVao")] // Map từ "MaKhoVao" trên RabbitMQ
        public int MaKho { get; set; }

        [JsonPropertyName("MaDiaChiLay")] // Map từ "MaDiaChiLay" trên RabbitMQ
        public int MaDiaChiLayHang { get; set; }

        public double KhoiLuong { get; set; } // Đảm bảo bên gửi có thuộc tính này
        public DateTime ThoiGian { get; set; }
    }

    // Dữ liệu hứng từ API Server Phương tiện
    public class VehicleFreeDto
    {
        public int MaPhuongTien { get; set; }
        public string? BienSo { get; set; }
        public double TaiTrongToiDaKg { get; set; }
        public string? TrangThai {  get; set; }
    }

    // Dữ liệu hứng từ API Server Nhân sự
    public class DriverAvailableDto
    {
        public int MaNguoiDung { get; set; }
        public string? HoTen { get; set; }
        public string? LoaiBangLai { get; set; }
        public double DiemUyTin { get; set; }
        public string? TrangThai { get; set; }
    }
    public class ClusterResult
    {
        public int MaDiaChiCum { get; set; }
        public int SoLuongDonHang { get; set; }
        public List<int>? DanhSachMaDonHang { get; set; }
        public double TongKhoiLuong { get; set; }
        public double TongTheTich { get; set; }
        public string? MaVungH3 { get; set; }
        public int MaDiaChiLayHang { get; set; }
    }
    public class UpdateTrangThaiXeDto
    {
        public string TrangThai { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO dùng để gửi cập nhật trạng thái Tài xế qua POST
    /// </summary>
    public class UpdateTaiXeTrangThaiDto
    {
        public int MaNguoiDung { get; set; }
        public string TrangThaiMoi { get; set; } = string.Empty;
    }
}