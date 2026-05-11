namespace QuanLyTaiKhoanNguoiDung.Models12.SeverQuanLyKhachHang.QuanLyKhachHang
{
    public class DiemThuongModels
    {
        public int? TongDiemTichLuy { get; set; }

        public int? DiemDaDung { get; set; }

        public DateTime? NgayCapNhatCuoi { get; set; }

        public virtual KhachHangModels? MaKhachHangNavigation { get; set; } 
    }
}
