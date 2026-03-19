namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyNhatKyHeThong
{
    public class LogMessage
    {
        public string? TenDichVu { get; set; }      
        public string? LoaiThaoTac { get; set; } // Ví dụ: "Sửa lịch làm việc", "Cập nhật User"
        public string? MaDoiTuong { get; set; } // ID của bản ghi bị tác động
        public string? TenBangLienQuan { get; set; } // Ví dụ: "LichLamViec", "NguoiDung"
        public object? DuLieuCu { get; set; } // Dữ liệu trước khi sửa (Anonymous object hoặc JSON)
        public object? DuLieuMoi { get; set; } // Dữ liệu sau khi sửa
        public string? NguoiThucHien { get; set; } // Tên hoặc ID người sửa
        public string? DiaChiIp { get; set; }
        public bool TrangThaiThaoTac { get; set; } = true;
        public DateTime ThoiGianThucHien { get; set; } = DateTime.Now;
    }
}
