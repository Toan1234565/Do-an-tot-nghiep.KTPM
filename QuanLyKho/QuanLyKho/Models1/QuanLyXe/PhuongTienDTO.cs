using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;

namespace QuanLyKho.Models1.QuanLyXe
{
    public class PhuongTienDTO
    {
        public int MaPhuongTien { get; set; }

        public string? BienSo { get; set; }

        public double? TaiTrongToiDaKg { get; set; }

        public double? TheTichToiDaM3 { get; set; }

        public string? TenLoaiXe { get; set; }

        public string? TenKho { get; set; }

        public double? MucTieuHaoNhienLieu { get; set; }

    }

        
}
