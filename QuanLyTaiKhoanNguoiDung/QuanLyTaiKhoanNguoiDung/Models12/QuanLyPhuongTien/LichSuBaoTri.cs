namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyPhuongTien
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

        public virtual PhuongTienModel? MaPhuongTienNavigation { get; set; }
    }
}
