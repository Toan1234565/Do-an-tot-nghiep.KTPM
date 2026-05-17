namespace QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.KhoBai
{
    // Request gửi đi chứa danh sách mã địa chỉ khách hàng
    public class BatchKhoRequest
    {
        public List<int> MaDiaChis { get; set; } = new();
    }

    // DTO nhận tọa độ phản hồi
    public class ToaDoResponseKhoDto
    {
        public int MaDiaChi { get; set; }
        public string? MaVungH3 { get; set; }
        public double? ViDo { get; set; }
        public double? KinhDo { get; set; }
    }

    // DTO chi tiết thông tin kho bãi tìm được
    public class KhoTimDuocDto
    {
        public int MaKho { get; set; }
        public string TenKho { get; set; } = string.Empty;
        public int MaDiaChi { get; set; }
        public string? MaVungH3 { get; set; }
        public string Distance { get; set; } = string.Empty;
        public int LoaiKho { get; set; }
    }
    public class KhoBaiDetailModel
    {
        public int MaKho { get; set; }
        public int MaDiaChi { get; set; }
        public string? TenKhoBai { get; set; }
        public int? MaQuanLy { get; set; }
        public double? DungTichM3 { get; set; }
        public double? DienTichM2 { get; set; }
        public string? SoDienThoaiKho { get; set; }
        public string? TrangThai { get; set; }
        public double? SucChua { get; set; }
        public int? MaLoaiKho { get; set; }
        public string? TenLoaiKho { get; set; }
    }
}
