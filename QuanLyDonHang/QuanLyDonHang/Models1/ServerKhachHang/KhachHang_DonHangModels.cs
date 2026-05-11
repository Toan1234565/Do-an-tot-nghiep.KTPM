namespace QuanLyDonHang.Models1.ServerKhachHang
{
    public class KhachHang_DonHangModels
    {
        public int MaDonHang { get; set; }
        public string? TenDonHang { get; set; }
       
        public decimal? TongTienThucTe { get; set; }
        
        public string? TrangThaiHienTai { get; set; }
        public DateTime ThoiGianTao { get; set; }
    }
}
