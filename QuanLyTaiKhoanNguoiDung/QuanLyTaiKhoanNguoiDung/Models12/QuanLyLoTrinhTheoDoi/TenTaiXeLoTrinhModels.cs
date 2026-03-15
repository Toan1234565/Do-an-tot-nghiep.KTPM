using Newtonsoft.Json;

namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyLoTrinhTheoDoi
{
    public class TenTaiXeLoTrinhModels
    {
        public int MaNguoiDung { get; set; }
        [JsonProperty("tenTaiXeThucHien")]
        public string? TenTaiXeThucHien { get; set; }
    }
}
