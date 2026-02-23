namespace QuanLyDonHang.Models1
{
    public class DonHangCreate
    {
        public string? SoDienThoai { get; set; }
        public string? TenKhachHang { get; set; }
        public DiaChiModels? DiaChiLay { get; set; }
        public DiaChiModels? DiaChiGiao { get; set; }
        public List<KienHangDTO>? DanhSachKienHang { get; set; }
        public string? TenNguoiNhan { get; set; }
        public string? SdtNguoiNhan { get; set; }

        public int? MaMucDoChon { get; set; }

        // Mã code khuyến mãi (Ví dụ: "GIAMGIA10", "FREESHIP")
        public string? MaKhuyenMaiCode { get; set; }

        // Số điểm mà khách hàng chọn nhập vào để trừ tiền
        public int SoDiemDoi { get; set; } = 0;

        // Ghi chú cho shipper hoặc hệ thống
        public string? GhiChu { get; set; }

        public int? MaMucDoDv { get; set; }

        public decimal? TongTienDuKien { get; set; }

        public decimal? TongTienThucTe { get; set; }
        public string? MaGiamGia { get; set; } 
    }
}