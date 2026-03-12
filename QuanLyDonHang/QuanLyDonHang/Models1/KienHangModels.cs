using Microsoft.Identity.Client;
using QuanLyDonHang.Models;

namespace QuanLyDonHang.Models1
{
    public class KienHangModels
    {
        public int MaKienHang { get; set; }

        public int MaDonHang { get; set; }

        public string? MaVach { get; set; }

       
        public double? KhoiLuong { get; set; }

        public double? TheTich { get; set; }

        public bool DaThuGom { get; set; }

        public decimal? SoTien { get; set; }

        public int SoLuongKienHang { get; set; }

        public int? MaKhoHienTai { get; set; }

        public int? MaLoaiHang { get; set; } 

        public int? MaBangGiaVung { get; set; }

        public virtual BangChungGiaoHang? BangChungGiaoHang { get; set; }

        public virtual ICollection<CapNhatTrangThai> CapNhatTrangThais { get; set; } = new List<CapNhatTrangThai>();

        public virtual DonHangModels MaDonHangNavigation { get; set; } = null!;

        public virtual DanhMucLoaiHangModels? MaLoaiHangNavigation { get; set; }
    }
}
