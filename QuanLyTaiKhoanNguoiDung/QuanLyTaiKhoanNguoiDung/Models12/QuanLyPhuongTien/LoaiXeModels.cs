using Newtonsoft.Json;

namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyPhuongTien
{
    public class LoaiXeModels
    {
        public int MaLoaiXe { get; set; }

        

        
        public string? TenLoai { get; set; }

        [JsonProperty("DanhSachHangMuc")]
        public List<DinhMucModels>? DanhSachHangMuc { get; set; }

        public virtual ICollection<DinhMucModels> DinhMucBaoTris { get; set; } = new List<DinhMucModels>();

       

        public virtual ICollection<PhuongTienModel> PhuongTiens { get; set; } = new List<PhuongTienModel>();
    }
}
