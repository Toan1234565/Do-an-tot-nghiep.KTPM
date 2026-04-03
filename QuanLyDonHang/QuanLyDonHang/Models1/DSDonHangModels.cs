namespace QuanLyDonHang.Models1
{
    public class DSDonHangModels
    {
        public int MaDonHang { get; set; }

        public int MaKhachHang { get; set; }

        public DateTime ThoiGianTao { get; set; }

        public int MaDiaChiNhanHang { get; set; }

        public string? TrangThaiHienTai { get; set; }

        public string? TenDonHang { get; set; }

        public int? MaLoaiDv { get; set; }
       
        public bool? LaDonGiaoThang { get; set; }

        public int? MaDiaChiLayHang { get; set; }     
    }
}
