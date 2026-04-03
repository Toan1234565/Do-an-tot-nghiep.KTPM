namespace QuanLyLoTrinhTheoDoi.Models12.DieuPhoiLoTrinh
{
    public class XacNhanLayHangRequest
    {
        public int MaLoTrinh { get; set; }
        public List<int>? DanhSachMaDonHang { get; set; }
        public int MaKhoHienTai { get; set; } // Kho vừa lấy hàng xong
    }
}
