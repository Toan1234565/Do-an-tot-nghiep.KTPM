namespace QuanLyDonHang.Models1.QuanLyDieuPhoiGomHang
{
    public class ClusterResult
    {
        public int MaDiaChiCum { get; set; }
        public int SoLuongDonHang { get; set; }
        public List<int>? DanhSachMaDonHang { get; set; }
        public double TongKhoiLuong { get; set; }
        public double TongTheTich { get; set; }
    }
}
