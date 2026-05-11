using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyTaiSan.QuanLyPhuongTien;

namespace QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyTaiSan.QuanLyDinhMucBaoTri
{
    public class LichSuBaoTri
    {
        public int MaBanGhi { get; set; }

        public int MaPhuongTien { get; set; }

        public decimal? ChiPhi { get; set; }

        public DateOnly? Ngay { get; set; }

        public double? SoKmThucTe { get; set; }

        public int? MaDinhMuc { get; set; }

        public string? LoaiBaoTri { get; set; }

        public virtual DinhMucModels? MaDinhMucNavigation { get; set; }

        public virtual BaoTriModel? MaPhuongTienNavigation { get; set; }
    }
}
