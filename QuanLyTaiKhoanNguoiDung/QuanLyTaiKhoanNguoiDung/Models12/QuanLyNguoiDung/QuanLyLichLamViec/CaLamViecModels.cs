using QuanLyTaiKhoanNguoiDung.Models;

namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyLichLamViec
{
    public class CaLamViecModels
    {
        public int MaCa { get; set; }

        public string? TenCa { get; set; }

        public TimeOnly? GioBatDau { get; set; }

        public TimeOnly? GioKetThuc { get; set; }

        public int? MaKho { get; set; }

        public virtual ICollection<DangKyCaTrucModels> DangKyCaTrucs { get; set; } = new List<DangKyCaTrucModels>();
    }
}
