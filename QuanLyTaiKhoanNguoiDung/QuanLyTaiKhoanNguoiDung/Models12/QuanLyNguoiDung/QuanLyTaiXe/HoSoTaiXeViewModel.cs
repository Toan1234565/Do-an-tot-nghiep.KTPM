namespace QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyTaiXe
{
    public class HoSoTaiXeViewModel
    {
        public ChiTietTaiXeModels? ChiTiet { get; set; }
        public List<LichSuViPhamModels> DanhSachViPham { get; set; } = new List<LichSuViPhamModels>();
    }
}
