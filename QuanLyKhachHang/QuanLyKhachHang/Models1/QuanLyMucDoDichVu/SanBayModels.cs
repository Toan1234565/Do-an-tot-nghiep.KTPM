using QuanLyKhachHang.Models;
using QuanLyKhachHang.Models1.QuanLyDiaChi;

namespace QuanLyKhachHang.Models1.QuanLyMucDoDichVu
{
    public class SanBayModels
    {
        public int MaSanBay { get; set; }

        public string IataCode { get; set; } = null!;

        public string TenSanBay { get; set; } = null!;

        public int MaDiaChi { get; set; }

        public bool? TrangThai { get; set; }

        public virtual DiaChiModels MaDiaChiNavigation { get; set; } = null!;
    }
}
