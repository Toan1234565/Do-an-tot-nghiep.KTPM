using QuanLyTaiKhoanNguoiDung.Models12.QuanLyDiaChi;

namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyKhachHang
{
    public class KhachHangModels
    {
        public int MaKhachHang { get; set; }

        public string TenCongTy { get; set; } = null!;

        public string? TenLienHe { get; set; }

        public string? SoDienThoai { get; set; }

        public string? Email { get; set; }

        public virtual ICollection<HopDongVanChuyenModels> HopDongVanChuyens { get; set; } = new List<HopDongVanChuyenModels>();

        public virtual DiaChiModel? MaDiaChiMacDinhNavigation { get; set; }

        public virtual ICollection<DiemThuongModels> DiemThuongs { get; set; } = new List<DiemThuongModels>();
    }
}
