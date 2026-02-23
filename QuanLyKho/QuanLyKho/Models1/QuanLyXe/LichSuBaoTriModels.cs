using QuanLyKho.Models;

namespace QuanLyKho.Models1.QuanLyXe
{
    public class LichSuBaoTriModels
    {
        public int MaBanGhi { get; set; }

        public int MaPhuongTien { get; set; }

        public decimal? ChiPhi { get; set; }

        public DateOnly? Ngay { get; set; }

        public double? SoKmThucTe { get; set; }

        public int? MaDinhMuc { get; set; }

        public string? LoaiBaoTri { get; set; }

        public virtual DinhMucBaoTri? MaDinhMucNavigation { get; set; }

        public virtual PhuongTien? MaPhuongTienNavigation { get; set; } 
    }
}
