using Newtonsoft.Json;

namespace QuanLyTaiKhoanNguoiDung.Models12.ServerQuanLyNguoiDung.QuanLyNguoiDung.QuanLyNhanVien
{
    public class TenNhanVienModel
    {
        public int MaNguoiDung { get; set; }
        [JsonProperty("tenTaiXeThucHien")]
        public string? TenTaiXeThucHien { get; set; }
    }
}
