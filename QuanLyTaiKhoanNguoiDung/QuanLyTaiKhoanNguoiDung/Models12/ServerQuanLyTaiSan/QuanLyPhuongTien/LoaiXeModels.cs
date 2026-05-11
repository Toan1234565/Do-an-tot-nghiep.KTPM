using Newtonsoft.Json;
using QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyTaiSan.QuanLyDinhMucBaoTri;

namespace QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyTaiSan.QuanLyPhuongTien
{
    public class LoaiXeModels
    {
        public int MaLoaiXe { get; set; }
        
        public string? TenLoai { get; set; }

        [JsonProperty("DanhSachHangMuc")]
        public List<DinhMucModels>? DanhSachHangMuc { get; set; }

        public virtual ICollection<DinhMucModels> DinhMucBaoTris { get; set; } = new List<DinhMucModels>();       
    }
}
