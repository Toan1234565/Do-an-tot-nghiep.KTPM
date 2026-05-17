namespace QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.PhuongTien
{
    // DTO nhận dữ liệu thông tin xe sẵn sàng điều phối
    public class PhuongTienDTO
    {
        public int MaPhuongTien { get; set; }
        public string? BienSo { get; set; }
        public double? TaiTrongToiDaKg { get; set; }
        public double? TheTichToiDaM3 { get; set; }
        public double? MucTieuHaoNhienLieu { get; set; }
        public string? TenLoaiXe { get; set; }
        public string? TenKho { get; set; }
    }

    // Request body để gửi trạng thái xe mới lên server
    public class UpdateTrangThaiXeDto
    {
        public string TrangThai { get; set; } = string.Empty;
    }

    // Response trả về sau khi cập nhật trạng thái xe thành công
    public class UpdateTrangThaiXeResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
    }
}
