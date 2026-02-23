using QuanLyKhachHang.Models;
using QuanLyKhachHang.Models1.QuanLyDiaChi;

namespace QuanLyKhachHang.Models1.QuanLyKhachHang
{
    public class KhachHangModels
    {
        public int MaKhachHang { get; set; }

        public string? TenCongTy { get; set; } 

        public string? TenLienHe { get; set; }

        public string? SoDienThoai { get; set; }

        public string? Email { get; set; }

        public virtual ICollection<HopDongVanChuyen> HopDongVanChuyens { get; set; } = new List<HopDongVanChuyen>();

        public virtual DiaChiModels? DiaChi { get; set; }

       

        public virtual ICollection<DiemThuongModels> DiemThuongs { get; set; } = new List<DiemThuongModels>();
    }
}
