using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tmdt.Shared.Services
{
    public class LogMessage
    {
        public string? TenDichVu { get; set; }
        public string? LoaiThaoTac { get; set; } // Ví dụ: "Sửa lịch làm việc", "Cập nhật User"
        public string? MaDoiTuong { get; set; } // ID của bản ghi bị tác động
        public string? TenBangLienQuan { get; set; } // Ví dụ: "LichLamViec", "NguoiDung"
        public object? DuLieuCu { get; set; } // Dữ liệu trước khi sửa (Anonymous object hoặc JSON)
        public object? DuLieuMoi { get; set; } // Dữ liệu sau khi sửa
        public int? MaNguoiDung { get; set; } // ID người thực hiện thao tác
        public string? NguoiThucHien { get; set; } // Tên hoặc ID người sửa
        public string? DiaChiIp { get; set; }
        public bool TrangThaiThaoTac { get; set; } = true;
        public DateTime ThoiGianThucHien { get; set; } = DateTime.Now;
    }
}
