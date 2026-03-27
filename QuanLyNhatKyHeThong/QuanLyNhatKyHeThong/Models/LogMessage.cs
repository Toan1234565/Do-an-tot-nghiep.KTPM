using System.Text.Json;

namespace QuanLyNhatKyHeThong.Models
{
    public class LogMessage
    {
        public string? TenDichVu { get; set; }
        public string? LoaiThaoTac { get; set; }
        public JsonElement DuLieuCu { get; set; } // Dùng JsonElement thay vì string
        public JsonElement DuLieuMoi { get; set; } // Dùng JsonElement thay vì string
        public string? NguoiThucHien { get; set; }
        public DateTime? ThoiGianThucHien { get; set; }
        public string? DiaChiIp { get; set; }
        public bool? TrangThaiThaoTac { get; set; }
    }
}
