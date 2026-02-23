namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyPhuongTien
{
    public class DinhMucModels
    {
        public int MaDinhMuc { get; set; }

        public int MaLoaiXe { get; set; }

        public string? TenHangMuc { get; set; }

        public double? DinhMucKm { get; set; }

        public int? DinhMucThang { get; set; }

        public virtual ICollection<LichSuBaoTri> LichSuBaoTris { get; set; } = new List<LichSuBaoTri>();

        public virtual LoaiXeModels? MaLoaiXeNavigation { get; set; }

       

    }
}
