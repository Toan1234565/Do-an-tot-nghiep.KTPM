using Microsoft.Identity.Client;
using QuanLyDonHang.Models;

namespace QuanLyDonHang.Models1
{
    public class KienHangModels
    {
       

        public string? MaVach { get; set; }

       
        public double? KhoiLuong { get; set; }

        public double? TheTich { get; set; }

        public bool DaThuGom { get; set; }

        public decimal? SoTien { get; set; }

        public int SoLuongKienHang { get; set; }

        

       

        public int? MaBangGiaVung { get; set; }

       

        

        public virtual DanhMucLoaiHangModels? MaLoaiHangNavigation { get; set; }
    }
}
