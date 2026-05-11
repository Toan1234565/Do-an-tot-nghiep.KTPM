using System.Text.Json.Serialization;

namespace QuanLyDonHang.Models1
{
    public class DonHangCreate
    {
        public string? TenDonHang { get; set; }
        public string? SoDienThoai { get; set; }
        public string? TenKhachHang { get; set; }
        public DiaChiModels? DiaChiLay { get; set; }
        public DiaChiModels? DiaChiGiao { get; set; }
        public DiaChiModels? H3Giao { get; set; }
        public DiaChiModels? H3Nhan {  get; set; }
        public List<KienHangDTO>? DanhSachKienHang { get; set; }
        public string? TenNguoiNhan { get; set; }
        public string? SdtNguoiNhan { get; set; }
        [JsonPropertyName("maMucDoDv")]
        public int? MaMucDoDv { get; set; }

        public decimal? TongTienDuKien { get; set; }

        public decimal? TongTienThucTe { get; set; }

        public int MaKhuyenMai { get; set; }

        public int MaPTTT { get; set; }

        public int? MaKhoHienTai { get; set; }


        public string? MaGiamGia { get; set; } 

        public int MaBangGiaVung { get; set; }

        public int SoLuongKienHang  { get; set; }

        public string? YeuCauBaoQuan { get; set; }
   
        public double? SoKm { get; set; }
    }
}