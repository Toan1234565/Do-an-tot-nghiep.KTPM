namespace QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.TaiXe
{
    // DTO gửi yêu cầu cập nhật trạng thái hoạt động của tài xế
    public class UpdateTaiXeTrangTai
    {
        public int MaNguoiDung { get; set; }
        public string TrangThaiMoi { get; set; } = string.Empty;
    }

    // DTO phản hồi sau khi cập nhật trạng thái thành công
    public class UpdateTrangThaiTaiXeResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }
    }

    // DTO nhận dữ liệu kiểm tra tính sẵn sàng và đi làm của tài xế
    public class DriverStatusResponseDto
    {
        public int MaNguoiDung { get; set; }
        public bool DangDiLam { get; set; }
        public bool SanSang { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsWorking { get; internal set; }
    }
    // Model mô tả chi tiết một ca làm việc
    public class CaLamViecModels
    {
        public int MaCa { get; set; }
        public string? TenCa { get; set; }
        public TimeOnly? GioBatDau { get; set; }  // Hoặc dùng TimeSpan tùy thuộc cấu hình dưới DB của bạn
        public TimeOnly? GioKetThuc { get; set; }
    }

    // Model bọc phản hồi tiêu chuẩn { success, data } từ Controller
    public class CaLamViecResponseApiResponse
    {
        public bool Success { get; set; }
        public List<CaLamViecModels>? Data { get; set; }
    }
}
