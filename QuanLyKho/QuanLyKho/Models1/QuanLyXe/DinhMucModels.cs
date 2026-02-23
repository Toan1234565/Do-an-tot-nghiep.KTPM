using QuanLyKho.Models;

namespace QuanLyKho.Models1.QuanLyXe
{
    public class DinhMucModels
    {
        public int MaDinhMuc { get; set; }

        public int MaLoaiXe { get; set; }

        public string? TenHangMuc { get; set; }

        public double? DinhMucKm { get; set; }

        public int? DinhMucThang { get; set; }

        public virtual ICollection<LichSuBaoTriModels> LichSuBaoTris { get; set; } = new List<LichSuBaoTriModels>();

        public virtual LoaiXe? MaLoaiXeNavigation { get; set; } 

        public string? TenLoaiXe { get; set; }
    }
}
