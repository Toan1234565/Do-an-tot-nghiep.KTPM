using QuanLyTaiKhoanNguoiDung.Models12.QuanLyDiaChi;

namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyDonHang
{
    public class DonHangCreate
    {
        // Thông tin khách hàng & Người nhận
        public int MaKhachHang { get; set; }
        public string TenNguoiNhan { get; set; } = null!;
        public string SdtNguoiNhan { get; set; } = null!;

        // Thông tin địa chỉ (ID từ bảng DiaChi)
        public int MaDiaChiNhanHang { get; set; } // Điểm lấy hàng
        public int MaDiaChiGiao { get; set; }     // Điểm giao hàng

        // Thông tin bổ sung
        public string? TenDonHang { get; set; }
        public string? GhiChuDacBiet { get; set; }
        public int? MaLoaiDv { get; set; }
        public int? MaVung { get; set; } // MaBangGia lấy từ API tính phí

        public string? SoDienThoaiGui { get; set; }
        public string? TenNguoiGui { get; set; }
        public DiaChiModel? DiaChiLay { get; set; }
        public DiaChiModel? DiaChiGiao { get; set; }
        // Danh sách kiện hàng
        public List<KienHangDTO> DanhSachKienHang { get; set; } = new List<KienHangDTO>();
     
    }
}
